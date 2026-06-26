using GMS.Web.Models.Entities;
using System.Collections.Generic;

namespace GMS.Web.Models.ViewModels
{
    public class StaffNotificationsViewModel
    {
        public List<Notification> Notifications { get; set; } = new();
        public int UnreadCount { get; set; }
    }
}
