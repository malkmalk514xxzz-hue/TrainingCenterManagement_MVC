using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrainingCenterManagement_MVC.Models
{
    public class GroupMessage
    {
        public int Id { get; set; }
        public Guid CourseId { get; set; }
        public Course Course { get; set; }
        public string SenderId { get; set; }
        public ApplicationUser Sender { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
