using HabitNest.Data;
using HabitNest.Models;
using Microsoft.EntityFrameworkCore;

namespace HabitNest.Services
{
    public class HabitService
    {
        public Habit AddHabit(string name, int userId, int durationDays)
        {
            using AppDbContext db = new AppDbContext();

            User? user = db.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Пользователь не найден"
                    );
            }

            if (durationDays <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationDays),
                    "Продолжительность должна быть больше нуля"
                    );
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Название привычки не может быть пустым",
                    nameof(name)
                );
            }

            string cleanName = name.Trim();

            if (db.Habits.Any(h => h.UserId == userId && h.Name.Trim() == cleanName))
            {
                throw new ArgumentException(
                    "У тебя уже есть привычка с таким названием.",
                    nameof(name)
                );
            }

            Habit habit = new Habit(cleanName, userId, durationDays);

            db.Habits.Add(habit);
            db.SaveChanges();

            return habit;
        }

        public void EditHabit(int habitId, string name, int userId, int durationDays)
        {
            using AppDbContext db = new AppDbContext();

            User? user = db.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Пользователь не найден"
                    );
            }

            if (durationDays <= 0)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(durationDays),
                    "Продолжительность должна быть больше нуля"
                    );
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException(
                    "Название привычки не может быть пустым",
                    nameof(name)
                );
            }

            Habit? habit = db.Habits.FirstOrDefault(h => h.Id == habitId && h.UserId == userId);

            if (habit == null)
            {
                throw new InvalidOperationException(
                    "Привычка не найдена или не принадлежит пользователю"
                );
            }
            string cleanName = name.Trim();

            if (db.Habits.Any(h => h.UserId == userId && h.Id != habitId && h.Name.Trim() == cleanName))
            {
                throw new ArgumentException(
                    "У тебя уже есть привычка с таким названием.",
                    nameof(name)
                );
            }

            DateTime? lastCompletionDate = db.HabitCompletions
                .Where(c => c.HabitId == habitId)
                .OrderByDescending(c => c.CompletionDate)
                .Select(c => (DateTime?)c.CompletionDate)
                .FirstOrDefault();

            if (lastCompletionDate.HasValue)
            {
                int minimumDurationDays =
                    (lastCompletionDate.Value.Date - habit.StartDate.Date).Days + 1;

                if (durationDays < minimumDurationDays)
                {
                    throw new ArgumentException(
                        $"У этой привычки есть отметка за {lastCompletionDate.Value:dd.MM.yyyy}. " +
                        $"Чтобы сохранить её в сроке привычки, введи не меньше {minimumDurationDays} дней."
                    );
                }
            }

            habit.Name = cleanName;
            habit.DurationDays = durationDays;
            db.SaveChanges();
        }

        public void DeleteHabit(int habitId, int userId)
        {
            using AppDbContext db = new AppDbContext();

            User? user = db.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Пользователь не найден"
                    );
            }

            Habit? habit = db.Habits.FirstOrDefault(h => h.Id == habitId && h.UserId == userId);

            if (habit == null)
            {
                throw new InvalidOperationException(
                    "Привычка не найдена или не принадлежит пользователю"
                );
            }

            db.Habits.Remove(habit);
            db.SaveChanges();
        }

        public HabitCompletion MarkAsCompleted(int habitId, int userId)
        {
            using AppDbContext db = new AppDbContext();

            User? user = db.Users.FirstOrDefault(u => u.Id == userId);

            if (user == null)
            {
                throw new InvalidOperationException(
                    "Пользователь не найден"
                    );
            }

            Habit? habit = db.Habits.FirstOrDefault(h => h.Id == habitId && h.UserId == userId);

            if (habit == null)
            {
                throw new InvalidOperationException(
                    "Привычка не найдена или не принадлежит пользователю"
                );
            }

            DateTime today = DateTime.Today;

            bool alreadyCompleted = db.HabitCompletions.Any(c => c.HabitId == habitId && c.CompletionDate == today);

            if (alreadyCompleted)
            {
                throw new InvalidOperationException(
                    "Привычка сегодня уже выполнена"
                );
            }


            if (today < habit.StartDate.Date ||
                today >= habit.StartDate.Date.AddDays(habit.DurationDays))
            {
                throw new InvalidOperationException(
                    "Привычка ещё не началась или уже закончилась"
                );
            }

            HabitCompletion completion = new();
            completion.HabitId = habitId;
            completion.CompletionDate = today;
            habit.Completions.Add(completion);
            db.SaveChanges();

            return completion;
        }

        public List<Habit> GetHabits(int userId)
        {

            using AppDbContext db = new AppDbContext();

            return db.Habits.Include(h => h.Completions).Where(h => h.UserId == userId).ToList();
        }
    }
}
