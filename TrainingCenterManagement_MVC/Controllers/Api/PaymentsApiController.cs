using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TrainingCenterManagement_MVC.Data;
using TrainingCenterManagement_MVC.Models;

namespace TrainingCenterManagement_MVC.Controllers.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public PaymentsApiController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPayments()
        {
            var payments = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee).ThenInclude(t => t.User)
                .ToListAsync();

            return Ok(payments);
        }

        [HttpGet("{id:guid}")]
        public async Task<ActionResult<Payment>> GetPayment(Guid id)
        {
            var payment = await _context.Payments
                .Include(p => p.Course)
                .Include(p => p.Trainee)
                .FirstOrDefaultAsync(p => p.PaymentId == id);

            if (payment == null) return NotFound();
            return Ok(payment);
        }

        [HttpPost]
        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Receptionist,Admin")]
        public async Task<ActionResult<Payment>> CreatePayment([FromBody] Payment payment)
        {
            // منطق التحقق من المبلغ المتبقي موجود في الكود الأصلي
            var course = await _context.Courses.FindAsync(payment.CourseId);
            if (course == null) return NotFound("Course not found");

            var existingPayments = await _context.Payments
                .Where(p => p.TraineeId == payment.TraineeId && p.CourseId == payment.CourseId)
                .ToListAsync();

            decimal totalPaid = existingPayments.Sum(p => p.TotalAmount);

            if (totalPaid >= course.Price)
                return BadRequest(new { message = "The total amount to Course is Complete" });

            if (totalPaid + payment.TotalAmount > course.Price)
                return BadRequest(new { message = "Payment amount exceeds remaining balance" });

            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPayment), new { id = payment.PaymentId }, payment);
        }

        [Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Receptionist,Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> DeletePayment(Guid id)
        {
            var payment = await _context.Payments.FindAsync(id);
            if (payment == null) return NotFound();

            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}