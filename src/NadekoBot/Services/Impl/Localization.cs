using System;
using System.Globalization;
using NadekoBot.Services.Database.Models;
using NLog;

namespace NadekoBot.Services
{
    public class Localization : ILocalization
    {
        public string this[string key] => LoadCommandString(key);

        public string this[string key, ulong guildId] => GetResponseString(key, guildId);

        public static string LoadCommandString(string key)
        {
            var toReturn = Resources.CommandStrings.ResourceManager.GetString(key);
            return string.IsNullOrWhiteSpace(toReturn) ? key : toReturn;
        }

        public static string GetResponseString(string key, ulong serverId)
        {
            var toReturn = Resources.ResponseStrings.ResourceManager.GetString(key, GetGuildLanguage(serverId));
            return string.IsNullOrWhiteSpace(toReturn) ? key : toReturn;
        }

        private static CultureInfo GetGuildLanguage(ulong serverId)
        {
            GuildConfig guild;
            using (var uow = DbHandler.UnitOfWork())
            {
                guild = uow.GuildConfigs.For(serverId);
            }
            if (guild == null)
            {
                return new CultureInfo("EN");
            }
            try
            {
                return new CultureInfo(guild.Language);
            }
            catch
            {
                return new CultureInfo("EN"); //Fallback
            }
        }

        //private static string GetCommandString(string key)
        //{
        //    return key;
            //var resx = new List<DictionaryEntry>();
            //var fs = new StreamReader(File.OpenRead("./Strings.resx"));
            //Console.WriteLine(fs.ReadToEnd());
            //using (var reader = new ResourceReader(fs.BaseStream))
            //{
            //    List<DictionaryEntry> existing = new List<DictionaryEntry>();
            //    foreach (DictionaryEntry item in reader)
            //    {
            //        existing.Add(item);
            //    }
            //    var existingResource = resx.Where(r => r.Key.ToString() == key).FirstOrDefault();
            //    if (existingResource.Key == null)
            //    {
            //        resx.Add(new DictionaryEntry() { Key = key, Value = key });
            //    }
            //    else
            //        return existingResource.Value.ToString();
            //}
            //using (var writer = new ResourceWriter(new FileStream("./Strings.resx", FileMode.OpenOrCreate)))
            //{
            //    resx.ForEach(r =>
            //    {
            //        writer.AddResource(r.Key.ToString(), r.Value.ToString());
            //    });
            //    writer.Generate();
            //}
            //return key;
        //}
    }
}
