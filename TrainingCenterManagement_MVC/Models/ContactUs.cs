using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TrainingCenterManagement_MVC.Models
{
    public class ContactUs
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string GuestId { get; set; }

        public List<GusetMessage> GusetMessages { get; set; } = new List<GusetMessage>();

        [DataType(DataType.DateTime)]
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}