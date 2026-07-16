using DAWEB.Models;
using DAWEB.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DAWEB.Areas.Admin.Controllers
{
    [Authorize(Roles = "Admin")]
    [Area("Admin")]
    public class VoucherController : Controller
    {
        private readonly DataContext _context;

        public VoucherController(DataContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var vouchers = await _context.Vouchers.OrderByDescending(v => v.Id).ToListAsync();
            return View(vouchers);
        }

        [HttpGet]
        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(VoucherModel voucher)
        {
            if (ModelState.IsValid)
            {
                // Convert Code to uppercase
                voucher.Code = voucher.Code.ToUpper();
                
                // Check if Code already exists
                if (await _context.Vouchers.AnyAsync(v => v.Code == voucher.Code))
                {
                    ModelState.AddModelError("Code", "Mã Voucher đã tồn tại.");
                    return View(voucher);
                }

                _context.Vouchers.Add(voucher);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Thêm Voucher thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(voucher);
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return NotFound();
            return View(voucher);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, VoucherModel voucher)
        {
            if (id != voucher.Id) return NotFound();

            if (ModelState.IsValid)
            {
                voucher.Code = voucher.Code.ToUpper();
                // Check if Code already exists (excluding current voucher)
                if (await _context.Vouchers.AnyAsync(v => v.Code == voucher.Code && v.Id != voucher.Id))
                {
                    ModelState.AddModelError("Code", "Mã Voucher đã tồn tại.");
                    return View(voucher);
                }

                _context.Vouchers.Update(voucher);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cập nhật Voucher thành công!";
                return RedirectToAction(nameof(Index));
            }
            return View(voucher);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var voucher = await _context.Vouchers.FindAsync(id);
            if (voucher == null) return NotFound();

            // Check if voucher was used, if yes we shouldn't hard delete it, just set IsActive = false
            if (voucher.UsedCount > 0)
            {
                voucher.IsActive = false;
                _context.Vouchers.Update(voucher);
                TempData["Success"] = "Voucher đã được sử dụng nên được chuyển sang trạng thái Vô hiệu hóa thay vì xóa.";
            }
            else
            {
                _context.Vouchers.Remove(voucher);
                TempData["Success"] = "Xóa Voucher thành công!";
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
