using System;
using System.Collections.Generic;
using System.Text;

namespace HabitNest.Models
{
    public class Habit
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int DurationDays {  get; set; }
        public DateTime StartDate { get; set; } = DateTime.Today;
        public int UserId { get; set; }
        public List<HabitCompletion> Completions = new();

        public Habit() { }

        public Habit(string name, int userId, int durationDays)
        {
            Name = name;
            DurationDays = durationDays;
            UserId = userId;
        }
        public double GetPercentage()
        {
            if (DurationDays <= 0)
            {
                return 0;
            }
            else
            {
                return (double)Completions.Count / DurationDays * 100;
            }
        }

        public int GetCurrentStreak()
        {
            if (Completions.Count == 0)
            {
                return 0;
            }
            var dates = Completions
                .Select(c => c.CompletionDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            DateTime lastDate = dates[dates.Count - 1];

            if (lastDate < DateTime.Today.AddDays(-1))
            {
                return 0;
            }

            int count = 1;

            for (int i = dates.Count - 1; i > 0; i--)
            {
                if ((dates[i] - dates[i - 1]).Days == 1)
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        public int GetBestStreak()
        {
            if (Completions.Count == 0)
            {
                return 0;
            }

            var dates = Completions
                .Select(c => c.CompletionDate.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();

            int count = 1;
            int best = 1;

            for (int i = 1; i < dates.Count; i++)
            {
                if ((dates[i] - dates[i - 1]).Days == 1)
                {
                    count++;
                } else
                {
                    count = 1;
                }

                if (count > best)
                {
                    best = count;
                }
            }

            return best;
        }
    }
}
