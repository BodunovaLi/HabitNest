using System;
using System.Collections.Generic;
using System.Text;

namespace HabitNest.Models
{
    public class HabitCompletion
    {
        public int Id { get; set; }
        public int HabitId { get; set; }
        public DateTime CompletionDate { get; set; } = DateTime.Today;

    }
}
