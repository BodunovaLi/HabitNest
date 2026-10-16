using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;


namespace HabitNest.Models
{
    [Index(nameof(TelegramId), IsUnique = true)]
    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public long TelegramId {  get; set; }

        public List<Habit> Habits { get; set; } = new();

        public User() { }
        public User(string name, long telegramId)
        {
            Name = name;
            TelegramId = telegramId;
        }
    }
}
