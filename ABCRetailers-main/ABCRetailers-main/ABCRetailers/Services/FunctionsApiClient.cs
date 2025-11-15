using ABCRetailers.Models;
using System.Text;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.Net.Sockets;

namespace ABCRetailers.Services
{
    public class FunctionsApiClient : IFunctionsApi
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<FunctionsApiClient> _logger;
        private readonly string _baseUrl;
        private readonly IAzureStorageService _storage;
        private record CustomerDto(string Id, string Name, string Surname, string Username, string Email, string ShippingAddress);

        public FunctionsApiClient(HttpClient httpClient, IConfiguration configuration, ILogger<FunctionsApiClient> logger, IAzureStorageService storage)
        {
            _httpClient = httpClient;
            _logger = logger;
            _baseUrl = configuration["Functions:BaseUrl"] ?? "http://localhost:7071/api";
            _storage = storage;
        }

        // Customer operations
        public async Task<List<Customer>> GetCustomersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/customers");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var dtos = JsonSerializer.Deserialize<List<CustomerDto>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<CustomerDto>();
                return dtos.Select(MapFromDto).ToList();
            }
            catch (HttpRequestException) // Functions host unreachable
            {
                _logger.LogWarning("Functions host unreachable; falling back to direct table read for customers.");
                return await _storage.GetAllEntitiesAsync<Customer>();
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; using storage fallback for customers.");
                return await _storage.GetAllEntitiesAsync<Customer>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customers from Functions API");
                return new List<Customer>();
            }
        }

        public async Task<Customer?> GetCustomerAsync(string id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/customers/{id}");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var dto = JsonSerializer.Deserialize<CustomerDto>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return dto is null ? null : MapFromDto(dto);
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; reading single customer from table.");
                return await _storage.GetEntityAsync<Customer>("Customer", id);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; reading single customer from table.");
                return await _storage.GetEntityAsync<Customer>("Customer", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting customer {Id} from Functions API", id);
                return null;
            }
        }

        public async Task<Customer> CreateCustomerAsync(Customer customer)
        {
            try
            {
                var payload = new
                {
                    name = customer.FirstName,
                    surname = customer.LastName,
                    username = "",
                    email = customer.Email,
                    shippingAddress = customer.ShippingAddress
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/customers", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var dto = JsonSerializer.Deserialize<CustomerDto>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return dto is null ? customer : MapFromDto(dto);
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; creating customer directly in table as fallback.");
                customer.PartitionKey = customer.PartitionKey ?? "Customer";
                if (string.IsNullOrWhiteSpace(customer.RowKey)) customer.RowKey = Guid.NewGuid().ToString();
                return await _storage.AddEntityAsync(customer);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; creating customer in table.");
                customer.PartitionKey = customer.PartitionKey ?? "Customer";
                if (string.IsNullOrWhiteSpace(customer.RowKey)) customer.RowKey = Guid.NewGuid().ToString();
                return await _storage.AddEntityAsync(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating customer via Functions API");
                throw;
            }
        }

        public async Task<Customer> UpdateCustomerAsync(Customer customer)
        {
            try
            {
                var payload = new
                {
                    name = customer.FirstName,
                    surname = customer.LastName,
                    username = "",
                    email = customer.Email,
                    shippingAddress = customer.ShippingAddress
                };
                var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{_baseUrl}/customers/{customer.RowKey}", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var dto = JsonSerializer.Deserialize<CustomerDto>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return dto is null ? customer : MapFromDto(dto);
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; updating customer directly in table.");
                return await _storage.UpdateEntityAsync(customer);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; updating customer in table.");
                return await _storage.UpdateEntityAsync(customer);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating customer {Id} via Functions API", customer.RowKey);
                throw;
            }
        }

        public async Task DeleteCustomerAsync(string id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/customers/{id}");
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; deleting customer directly in table.");
                await _storage.DeleteEntityAsync<Customer>("Customer", id);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; deleting customer in table.");
                await _storage.DeleteEntityAsync<Customer>("Customer", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting customer {Id} via Functions API", id);
                throw;
            }
        }

        // Product operations
        public async Task<List<Product>> GetProductsAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/products");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<Product>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Product>();
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; falling back to direct table read for products.");
                return await _storage.GetAllEntitiesAsync<Product>();
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; using storage fallback for products.");
                return await _storage.GetAllEntitiesAsync<Product>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting products from Functions API");
                return new List<Product>();
            }
        }

        public async Task<Product?> GetProductAsync(string id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/products/{id}");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Product>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; reading single product from table.");
                return await _storage.GetEntityAsync<Product>("Product", id);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; reading single product from table.");
                return await _storage.GetEntityAsync<Product>("Product", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting product {Id} from Functions API", id);
                return null;
            }
        }

        public async Task<Product> CreateProductAsync(Product product)
        {
            try
            {
                var json = JsonSerializer.Serialize(product, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/products", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Product>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? product;
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; creating product directly in table.");
                product.PartitionKey = product.PartitionKey ?? "Product";
                if (string.IsNullOrWhiteSpace(product.RowKey)) product.RowKey = Guid.NewGuid().ToString();
                return await _storage.AddEntityAsync(product);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; creating product in table.");
                product.PartitionKey = product.PartitionKey ?? "Product";
                if (string.IsNullOrWhiteSpace(product.RowKey)) product.RowKey = Guid.NewGuid().ToString();
                return await _storage.AddEntityAsync(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product via Functions API");
                throw;
            }
        }

        public async Task<Product> UpdateProductAsync(Product product)
        {
            try
            {
                var json = JsonSerializer.Serialize(product, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{_baseUrl}/products/{product.RowKey}", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Product>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? product;
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; updating product directly in table.");
                return await _storage.UpdateEntityAsync(product);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; updating product in table.");
                return await _storage.UpdateEntityAsync(product);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating product {Id} via Functions API", product.RowKey);
                throw;
            }
        }

        public async Task DeleteProductAsync(string id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/products/{id}");
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; deleting product directly in table.");
                await _storage.DeleteEntityAsync<Product>("Product", id);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; deleting product in table.");
                await _storage.DeleteEntityAsync<Product>("Product", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting product {Id} via Functions API", id);
                throw;
            }
        }

        // Order operations
        public async Task<List<Order>> GetOrdersAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/orders");
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<List<Order>>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new List<Order>();
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; falling back to direct table read for orders.");
                return await _storage.GetAllEntitiesAsync<Order>();
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; using storage fallback for orders.");
                return await _storage.GetAllEntitiesAsync<Order>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting orders from Functions API");
                return new List<Order>();
            }
        }

        public async Task<Order?> GetOrderAsync(string id)
        {
            try
            {
                var response = await _httpClient.GetAsync($"{_baseUrl}/orders/{id}");
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                    return null;
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Order>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; reading single order from table.");
                return await _storage.GetEntityAsync<Order>("Order", id);
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; reading single order from table.");
                return await _storage.GetEntityAsync<Order>("Order", id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting order {Id} from Functions API", id);
                return null;
            }
        }

        public async Task<Order> CreateOrderAsync(Order order)
        {
            try
            {
                var json = JsonSerializer.Serialize(order, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_baseUrl}/orders", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Order>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? order;
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; creating order directly in table and queuing notification.");
                order.PartitionKey = order.PartitionKey ?? "Order";
                if (string.IsNullOrWhiteSpace(order.RowKey)) order.RowKey = Guid.NewGuid().ToString();
                var created = await _storage.AddEntityAsync(order);
                var message = JsonSerializer.Serialize(new { type = "order-created", orderId = created.RowKey, customerId = created.CustomerId, status = created.Status, total = created.TotalPrice });
                await _storage.SendMessageAsync("order-notifications", message);
                return created;
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; creating order in table and queuing notification.");
                order.PartitionKey = order.PartitionKey ?? "Order";
                if (string.IsNullOrWhiteSpace(order.RowKey)) order.RowKey = Guid.NewGuid().ToString();
                var created = await _storage.AddEntityAsync(order);
                var message = JsonSerializer.Serialize(new { type = "order-created", orderId = created.RowKey, customerId = created.CustomerId, status = created.Status, total = created.TotalPrice });
                await _storage.SendMessageAsync("order-notifications", message);
                return created;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating order via Functions API");
                throw;
            }
        }

        public async Task<Order> UpdateOrderAsync(Order order)
        {
            try
            {
                var json = JsonSerializer.Serialize(order, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PutAsync($"{_baseUrl}/orders/{order.RowKey}", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Order>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? order;
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; updating order directly in table and queuing notification.");
                var updated = await _storage.UpdateEntityAsync(order);
                var message = JsonSerializer.Serialize(new { type = "order-updated", orderId = updated.RowKey, status = updated.Status });
                await _storage.SendMessageAsync("order-notifications", message);
                return updated;
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; updating order in table and queuing notification.");
                var updated = await _storage.UpdateEntityAsync(order);
                var message = JsonSerializer.Serialize(new { type = "order-updated", orderId = updated.RowKey, status = updated.Status });
                await _storage.SendMessageAsync("order-notifications", message);
                return updated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order {Id} via Functions API", order.RowKey);
                throw;
            }
        }

        public async Task<Order> UpdateOrderStatusAsync(string id, string status)
        {
            try
            {
                var statusUpdate = new { status };
                var json = JsonSerializer.Serialize(statusUpdate, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                var response = await _httpClient.PatchAsync($"{_baseUrl}/orders/{id}/status", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<Order>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new Order();
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; updating order status directly in table and queuing notification.");
                var order = await _storage.GetEntityAsync<Order>("Order", id) ?? new Order { PartitionKey = "Order", RowKey = id };
                order.Status = status;
                var updated = await _storage.UpdateEntityAsync(order);
                var message = JsonSerializer.Serialize(new { type = "order-status-updated", orderId = updated.RowKey, status });
                await _storage.SendMessageAsync("order-notifications", message);
                return updated;
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; updating order status in table and queuing notification.");
                var order = await _storage.GetEntityAsync<Order>("Order", id) ?? new Order { PartitionKey = "Order", RowKey = id };
                order.Status = status;
                var updated = await _storage.UpdateEntityAsync(order);
                var message = JsonSerializer.Serialize(new { type = "order-status-updated", orderId = updated.RowKey, status });
                await _storage.SendMessageAsync("order-notifications", message);
                return updated;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating order status for {Id} via Functions API", id);
                throw;
            }
        }

        public async Task DeleteOrderAsync(string id)
        {
            try
            {
                var response = await _httpClient.DeleteAsync($"{_baseUrl}/orders/{id}");
                response.EnsureSuccessStatusCode();
            }
            catch (HttpRequestException)
            {
                _logger.LogWarning("Functions host unreachable; deleting order directly in table.");
                await _storage.DeleteEntityAsync<Order>("Order", id);
                await _storage.SendMessageAsync("order-notifications", JsonSerializer.Serialize(new { type = "order-deleted", orderId = id }));
            }
            catch (SocketException)
            {
                _logger.LogWarning("Socket exception contacting Functions; deleting order in table.");
                await _storage.DeleteEntityAsync<Order>("Order", id);
                await _storage.SendMessageAsync("order-notifications", JsonSerializer.Serialize(new { type = "order-deleted", orderId = id }));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting order {Id} via Functions API", id);
                throw;
            }
        }

        // Upload operations
        public async Task<string> UploadFileAsync(IFormFile file, string containerName)
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var stream = file.OpenReadStream();
                content.Add(new StreamContent(stream), "file", file.FileName);
                content.Add(new StringContent(containerName), "containerName");

                var response = await _httpClient.PostAsync($"{_baseUrl}/uploads", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, string>>(responseJson);
                return result?["fileName"] ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading file via Functions API");
                throw;
            }
        }

        public async Task<string> UploadToFileShareAsync(IFormFile file, string shareName, string directoryName = "")
        {
            try
            {
                using var content = new MultipartFormDataContent();
                using var stream = file.OpenReadStream();
                content.Add(new StreamContent(stream), "file", file.FileName);
                content.Add(new StringContent(shareName), "shareName");
                content.Add(new StringContent(directoryName), "directoryName");

                var response = await _httpClient.PostAsync($"{_baseUrl}/uploads/fileshare", content);
                response.EnsureSuccessStatusCode();
                var responseJson = await response.Content.ReadAsStringAsync();
                var result = JsonSerializer.Deserialize<Dictionary<string, string>>(responseJson);
                return result?["fileName"] ?? "";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error uploading to file share via Functions API");
                throw;
            }
        }

        private static Customer MapFromDto(CustomerDto dto)
        {
            return new Customer
            {
                PartitionKey = "Customer",
                RowKey = dto.Id,
                FirstName = dto.Name,
                LastName = dto.Surname,
                Email = dto.Email,
                ShippingAddress = dto.ShippingAddress
            };
        }
    }
}
