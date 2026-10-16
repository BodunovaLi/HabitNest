using System;
using System.Collections.Generic;
using System.Text;

namespace HabitNest.Bot
{
    public enum UserState
    {
        None,
        WaitingForUserName,
        WaitingForHabitName,
        WaitingForHabitDuration,
        WaitingForEditHabitName,
        WaitingForEditHabitDuration,

        WaitingForNewUserName
    }
}
