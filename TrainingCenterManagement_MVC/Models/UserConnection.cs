using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrainingCenterManagement_MVC.Models
{
    public class UserConnection
    {
        public int Id { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public string ConnectionId { get; set; }
        public DateTime ConnectedAt { get; set; }
        public bool IsConnected { get; set; }
    }
}
