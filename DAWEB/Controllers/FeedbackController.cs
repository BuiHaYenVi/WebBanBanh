using Microsoft.AspNetCore.Mvc;
using DAWEB.Repository;
using DAWEB.Models;
using System.Threading.Tasks;
using System;

namespace DAWEB.Controllers
{
    public class FeedbackController : Controller
    {
        private readonly DataContext _context;

        public FeedbackController(DataContext context)
        {
            _context = context;
        }

        [HttpPost]
        public async Task<IActionResult> Submit(FeedbackModel feedback)
        {
            if (ModelState.IsValid)
            {
                feedback.CreatedDate = DateTime.Now;
                feedback.IsRead = false;
                _context.Feedbacks.Add(feedback);
                await _context.SaveChangesAsync();
                
                TempData["SuccessMessage"] = "Góp ý của bạn đã được gửi. SweetBite sẽ liên hệ với bạn trong thời gian sớm nhất!";
            }
            else
            {
                TempData["ErrorMessage"] = "Vui lòng nhập đầy đủ thông tin góp ý.";
            }

            return Redirect(Request.Headers["Referer"].ToString() ?? "/");
        }
    }
}
