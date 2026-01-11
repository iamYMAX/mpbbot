using System.Collections.Concurrent;

namespace TelegramGigaChatBot.Services;

public enum UserAction
{
    None,
    DictatingEmailReply,
    EditingEmailReply,
    EnteringCustomReplyStyle
}

public class UserActionState
{
    public UserAction CurrentAction { get; set; } = UserAction.None;
    public string Data { get; set; }
}

public class UserActionStateService
{
    private readonly ConcurrentDictionary<long, UserActionState> _userStates = new();

    public void SetState(long userId, UserAction action, string data = null)
    {
        var state = new UserActionState { CurrentAction = action, Data = data };
        _userStates.AddOrUpdate(userId, state, (key, oldState) => state);
    }

    public UserActionState GetState(long userId)
    {
        return _userStates.GetValueOrDefault(userId, new UserActionState());
    }

    public void ClearState(long userId)
    {
        _userStates.TryRemove(userId, out _);
    }
}
