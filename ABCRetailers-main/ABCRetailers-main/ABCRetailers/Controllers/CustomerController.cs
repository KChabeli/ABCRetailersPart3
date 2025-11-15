using Microsoft.AspNetCore.Mvc;
using ABCRetailers.Models;
using ABCRetailers.Services;

namespace ABCRetailers.Controllers
{
    public class CustomerController : Controller
    {
        private readonly IFunctionsApi _functionsApi;

        public CustomerController(IFunctionsApi functionsApi)
        {
            _functionsApi = functionsApi;
        }

        // GET: /Customer
        public async Task<IActionResult> Index(string searchString)
        {
            var customers = await _functionsApi.GetCustomersAsync();

            if (!string.IsNullOrEmpty(searchString))
            {
                customers = customers.Where(c =>
                    c.FirstName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) == true ||
                    c.LastName?.Contains(searchString, StringComparison.OrdinalIgnoreCase) == true ||
                    c.Email?.Contains(searchString, StringComparison.OrdinalIgnoreCase) == true
                ).ToList();
            }

            ViewBag.SearchString = searchString;
            return View(customers);
        }

        // GET: /Customer/Create
        public IActionResult Create()
        {
            return View(new Customer());
        }

        // POST: /Customer/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer model)
        {
            if (!ModelState.IsValid)
                return View(model);

            // RowKey must be unique
            model.RowKey = Guid.NewGuid().ToString();
            model.PartitionKey = "Customer";

            await _functionsApi.CreateCustomerAsync(model);
            TempData["Message"] = "Customer created successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Customer/Edit/{id}
        public async Task<IActionResult> Edit(string id)
        {
            if (id == null) return BadRequest();

            var customer = await _functionsApi.GetCustomerAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        // POST: /Customer/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Customer model)
        {
            if (!ModelState.IsValid)
                return View(model);

            await _functionsApi.UpdateCustomerAsync(model);
            TempData["Message"] = "Customer updated successfully!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Customer/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            if (id == null) return BadRequest();

            await _functionsApi.DeleteCustomerAsync(id);
            TempData["Message"] = "Customer deleted successfully!";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Customer/Details/{id}
        public async Task<IActionResult> Details(string id)
        {
            if (id == null) return BadRequest();

            var customer = await _functionsApi.GetCustomerAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }
    }
}

