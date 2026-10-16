using HabitNest.Data;
using HabitNest.Models;

namespace HabitNest.Services
{
    public class UserService
    {
        public User? GetByTelegramId(long telegramId)
        {
            using AppDbContext db = new AppDbContext();
            return db.Users.FirstOrDefault(u => u.TelegramId == telegramId);
        }


        public void DeleteUser(long telegramId)
        {
            using AppDbContext db = new AppDbContext();
            User? user = db.Users.FirstOrDefault(u => u.TelegramId == telegramId);
            if (user != null)
            {
                db.Users.Remove(user);
                db.SaveChanges();
            }
        }

        public User CreateUser(string name, long telegramId)
        {
            using AppDbContext db = new AppDbContext();

            User? existingUser = db.Users.FirstOrDefault(
                u => u.TelegramId == telegramId
            );

            if (existingUser != null)
            {
                return existingUser;
            }

            User user = new User(name, telegramId);

            db.Users.Add(user);
            db.SaveChanges();

            return user;
        }

        public User? ChangeName(string userName, long telegramId)
        {
            if (string.IsNullOrWhiteSpace(userName))
            {
                throw new ArgumentException("Имя не может быть пустым.");
            }

            using AppDbContext db = new AppDbContext();

            User? user = db.Users.FirstOrDefault(u => u.TelegramId == telegramId);

            if (user == null)
            {
                return null;
            }

            user.Name = userName.Trim();
            db.SaveChanges();

            return user;

        }
    }
}
