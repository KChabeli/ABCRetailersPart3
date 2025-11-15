using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using ABCRetailers.Models;
using ABCRetailers.Services;

namespace ABCRetailers.Controllers
{
    public class OrderController : Controller
    {
        private readonly IFunctionsApi _functionsApi;

        public OrderController(IFunctionsApi functionsApi)
        {
            _functionsApi = functionsApi;
        }

        // GET: /Order
        public async Task<IActionResult> Index(string searchString)
        {
            var orders = await _functionsApi.GetOrdersAsync();

            if (!string.IsNullOrEmpty(searchString))
            {
                orders = orders.Where(o =>
                    o.CustomerId?.Contains(searchString, StringComparison.OrdinalIgnoreCase) == true ||
                    o.ProductId?.Contains(searchString, StringComparison.OrdinalIgnoreCase) == true ||
                    o.Status?.Contains(searchString, StringComparison.OrdinalIgnoreCase) == true
                ).ToList();
            }

            // Populate customer and product names for display
            var customerNames = new Dictionary<string, string>();
            var productNames = new Dictionary<string, string>();

            try
            {
                // Get customer names
                var customers = await _functionsApi.GetCustomersAsync();
                foreach (var customer in customers)
                {
                    customerNames[customer.RowKey] = $"{customer.FirstName} {customer.LastName}";
                }

                // Get product names
                var products = await _functionsApi.GetProductsAsync();
                foreach (var product in products)
                {
                    productNames[product.RowKey] = product.ProductName;
                }
            }
            catch
            {
                // If there's an error fetching names, continue with empty dictionaries
            }

            ViewBag.CustomerNames = customerNames;
            ViewBag.ProductNames = productNames;
            ViewBag.SearchString = searchString;

            return View(orders);
        }

        // GET: /Order/Create
        public async Task<IActionResult> Create()
        {
            ViewBag.Customers = await GetCustomerSelectList();
            ViewBag.Products = await GetProductSelectList();
            var order = new Order();
            // Ensure the default date is UTC
            EnsureDateTimeIsUtc(order);
            return View(order);
        }

        // POST: /Order/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Order model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            // Fetch product to get price
            var product = await _functionsApi.GetProductAsync(model.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("ProductId", "Selected product not found.");
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            // Validate that the product has a valid price
            if (product.Price <= 0)
            {
                ModelState.AddModelError("ProductId", "Selected product has an invalid price.");
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            // Set the unit price from the product and calculate total
            model.UnitPrice = product.Price;
            model.TotalPrice = model.UnitPrice * model.Quantity;

            // Ensure DateTime properties are UTC for Azure Table Storage
            EnsureDateTimeIsUtc(model);

            // Validate that the date is now properly UTC
            if (model.OrderDate.Kind != DateTimeKind.Utc)
            {
                ModelState.AddModelError("OrderDate", "Order date must be in UTC format for Azure storage.");
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            model.RowKey = Guid.NewGuid().ToString();
            model.PartitionKey = "Order";

            try
            {
                await _functionsApi.CreateOrderAsync(model);
                TempData["Message"] = "Order created successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error creating order: {ex.Message}";
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }
        }

        // GET: /Order/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return BadRequest();

            var order = await _functionsApi.GetOrderAsync(id);
            if (order == null) return NotFound();

            ViewBag.Customers = await GetCustomerSelectList();
            ViewBag.Products = await GetProductSelectList();

            return View(order);
        }

        // POST: /Order/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Order model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            // Update price/total in case product/qty changed
            var product = await _functionsApi.GetProductAsync(model.ProductId);
            if (product == null)
            {
                ModelState.AddModelError("ProductId", "Selected product not found.");
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            // Validate that the product has a valid price
            if (product.Price <= 0)
            {
                ModelState.AddModelError("ProductId", "Selected product has an invalid price.");
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            // Set the unit price from the product and calculate total
            model.UnitPrice = product.Price;
            model.TotalPrice = model.UnitPrice * model.Quantity;

            // Ensure DateTime properties are UTC for Azure Table Storage
            EnsureDateTimeIsUtc(model);

            // Validate that the date is now properly UTC
            if (model.OrderDate.Kind != DateTimeKind.Utc)
            {
                ModelState.AddModelError("OrderDate", "Order date must be in UTC format for Azure storage.");
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            // Validate that the price is set correctly
            if (model.UnitPrice <= 0)
            {
                ModelState.AddModelError("UnitPrice", "Product price must be greater than zero.");
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }

            try
            {
                await _functionsApi.UpdateOrderAsync(model);
                TempData["Message"] = "Order updated successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating order: {ex.Message}";
                ViewBag.Customers = await GetCustomerSelectList();
                ViewBag.Products = await GetProductSelectList();
                return View(model);
            }
        }

        // GET: /Order/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return BadRequest();

            var order = await _functionsApi.GetOrderAsync(id);
            if (order == null) return NotFound();

            // Fetch product details to show product name
            try
            {
                var product = await _functionsApi.GetProductAsync(order.ProductId);
                if (product != null)
                {
                    ViewBag.ProductName = product.ProductName;
                }
            }
            catch
            {
                // If product not found, just show the ID
                ViewBag.ProductName = order.ProductId;
            }

            // Fetch customer details to show customer name
            try
            {
                var customer = await _functionsApi.GetCustomerAsync(order.CustomerId);
                if (customer != null)
                {
                    ViewBag.CustomerName = $"{customer.FirstName} {customer.LastName}";
                }
            }
            catch
            {
                // If customer not found, just show the ID
                ViewBag.CustomerName = order.CustomerId;
            }

            return View(order);
        }

        // POST: /Order/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return BadRequest();

            try
            {
                await _functionsApi.DeleteOrderAsync(id);
                TempData["Message"] = "Order deleted successfully!";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error deleting order: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // GET: /Order/UpdateStatus/{id}
        public async Task<IActionResult> UpdateStatus(string id)
        {
            if (id == null) return BadRequest();

            var order = await _functionsApi.GetOrderAsync(id);
            if (order == null) return NotFound();

            ViewBag.Order = order;
            ViewBag.StatusOptions = new List<SelectListItem>
            {
                new SelectListItem { Value = "Submitted", Text = "Submitted" },
                new SelectListItem { Value = "Processing", Text = "Processing" },
                new SelectListItem { Value = "Shipped", Text = "Shipped" },
                new SelectListItem { Value = "Delivered", Text = "Delivered" },
                new SelectListItem { Value = "Cancelled", Text = "Cancelled" }
            };

            return View();
        }

        // POST: /Order/UpdateStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(string id, string status)
        {
            if (id == null || string.IsNullOrEmpty(status)) return BadRequest();

            try
            {
                await _functionsApi.UpdateOrderStatusAsync(id, status);
                TempData["Message"] = $"Order status updated to '{status}' successfully!";
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Error updating order status: {ex.Message}";
                return RedirectToAction(nameof(UpdateStatus), new { id });
            }
        }

        // =====================
        // Helpers
        // =====================
        private void EnsureDateTimeIsUtc(Order order)
        {
            // Ensure DateTime properties are UTC for Azure Table Storage
            if (order.OrderDate.Kind == DateTimeKind.Unspecified)
            {
                order.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);
            }
            else if (order.OrderDate.Kind == DateTimeKind.Local)
            {
                order.OrderDate = order.OrderDate.ToUniversalTime();
            }

            // Additional safety check - if somehow the date is still not UTC, force it
            if (order.OrderDate.Kind != DateTimeKind.Utc)
            {
                order.OrderDate = DateTime.SpecifyKind(order.OrderDate, DateTimeKind.Utc);
            }
        }

        private async Task<List<SelectListItem>> GetCustomerSelectList()
        {
            try
            {
                var customers = await _functionsApi.GetCustomersAsync();
                return customers.Select(c => new SelectListItem
                {
                    Value = c.RowKey,
                    Text = $"{c.FirstName} {c.LastName} ({c.Email})"
                }).ToList();
            }
            catch
            {
                return new List<SelectListItem>();
            }
        }

        private async Task<List<SelectListItem>> GetProductSelectList()
        {
            try
            {
                var products = await _functionsApi.GetProductsAsync();
                return products.Select(p => new SelectListItem
                {
                    Value = p.RowKey,
                    Text = $"{p.ProductName} - {p.Price:C}"
                }).ToList();
            }
            catch
            {
                return new List<SelectListItem>();
            }
        }
    }
}