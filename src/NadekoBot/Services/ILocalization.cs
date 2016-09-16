namespace NadekoBot.Services
{
    public interface ILocalization
    {
        string this[string key, ulong guildId] { get; }
    }
}
