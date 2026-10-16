using System;
using System.Collections.Generic;
using System.Text;

namespace HabitNest.Bot
{
    public class UserSession
    {
        public int? EditingHabitId { get; set; }
        public long TelegramId { get; set; }
        public UserState State { get; set; }
        public string? TempHabitName { get; set; }
    }
}
