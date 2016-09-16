using Discord;
using Discord.Commands;
using NadekoBot.Attributes;
using NadekoBot.Services;
using NadekoBot.Services.Database.Models;
using NLog;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace NadekoBot.Modules.Administration
{
    public partial class Administration
    {
        [Group]
        public class ServerGreetCommands
        {
            public static long Greeted = 0;
            private Logger _log;
            private ILocalization _l;

            public ServerGreetCommands()
            {
                _l = NadekoBot.Localizer;
                NadekoBot.Client.UserJoined += UserJoined;
                NadekoBot.Client.UserLeft += UserLeft;
                _log = LogManager.GetCurrentClassLogger();
            }

            private Task UserLeft(IGuildUser user)
            {
                var leftTask = Task.Run(async () =>
                {
                    GuildConfig conf;
                    using (var uow = DbHandler.UnitOfWork())
                    {
                        conf = uow.GuildConfigs.For(user.Guild.Id);
                    }

                    if (!conf.SendChannelByeMessage) return;
                    var channel = (await user.Guild.GetTextChannelsAsync()).SingleOrDefault(c => c.Id == conf.ByeMessageChannelId);

                    if (channel == null) //maybe warn the server owner that the channel is missing
                        return;

                    var msg = conf.ChannelByeMessageText.Replace("%user%", "**" + user.Username + "**");
                    if (string.IsNullOrWhiteSpace(msg))
                        return;

                    var toDelete = await channel.SendMessageAsync(msg).ConfigureAwait(false);
                    if (conf.AutoDeleteByeMessages)
                    {
                        var t = Task.Run(async () =>
                        {
                            await Task.Delay(conf.AutoDeleteGreetMessagesTimer * 1000).ConfigureAwait(false); // 5 minutes
                            await toDelete.DeleteAsync().ConfigureAwait(false);
                        });
                    }
                });
                return Task.CompletedTask;
            }

            private Task UserJoined(IGuildUser user)
            {
                var joinedTask = Task.Run(async () =>
                {
                    GuildConfig conf;
                    using (var uow = DbHandler.UnitOfWork())
                    {
                        conf = uow.GuildConfigs.For(user.Guild.Id);
                    }

                    if (conf.SendChannelGreetMessage)
                    {
                        var channel = (await user.Guild.GetTextChannelsAsync()).SingleOrDefault(c => c.Id == conf.GreetMessageChannelId);
                        if (channel != null) //maybe warn the server owner that the channel is missing
                        {
                            var msg = conf.ChannelGreetMessageText.Replace("%user%", user.Username).Replace("%server%", user.Guild.Name);
                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                var toDelete = await channel.SendMessageAsync(msg).ConfigureAwait(false);
                                if (conf.AutoDeleteGreetMessages)
                                {
                                    var t = Task.Run(async () =>
                                    {
                                        await Task.Delay(conf.AutoDeleteGreetMessagesTimer * 1000).ConfigureAwait(false); // 5 minutes
                                        await toDelete.DeleteAsync().ConfigureAwait(false);
                                    });
                                }
                            }
                        }
                    }

                    if (conf.SendDmGreetMessage)
                    {
                        var channel = await user.CreateDMChannelAsync();

                        if (channel != null)
                        {
                            var msg = conf.DmGreetMessageText.Replace("%user%", user.Username).Replace("%server%", user.Guild.Name);
                            if (!string.IsNullOrWhiteSpace(msg))
                            {
                                await channel.SendMessageAsync(msg).ConfigureAwait(false);
                            }
                        }
                    }
                });
                return Task.CompletedTask;
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task GreetDel(IUserMessage umsg)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    conf.AutoDeleteGreetMessages = !conf.AutoDeleteGreetMessages;
                    uow.GuildConfigs.Update(conf);
                    await uow.CompleteAsync();
                }

                if (conf.AutoDeleteGreetMessages)
                    await channel.SendMessageAsync(_l["administration_greetdel_enabled", channel.Guild.Id]).ConfigureAwait(false);
                else
                    await channel.SendMessageAsync(_l["administration_greetdel_disabled", channel.Guild.Id]).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task Greet(IUserMessage umsg)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    conf.SendChannelGreetMessage = !conf.SendChannelGreetMessage;
                    conf.GreetMessageChannelId = channel.Id;
                    uow.GuildConfigs.Update(conf);
                    await uow.CompleteAsync();
                }

                if (conf.SendChannelGreetMessage)
                    await channel.SendMessageAsync(_l["administration_greet_enabled", channel.Guild.Id]).ConfigureAwait(false);
                else
                    await channel.SendMessageAsync(_l["administration_greet_disabled", channel.Guild.Id]).ConfigureAwait(false);
            }
            
            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task GreetMsg(IUserMessage umsg, [Remainder] string text)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        conf.ChannelGreetMessageText = text;
                        uow.GuildConfigs.Update(conf);
                        await uow.CompleteAsync();
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    await channel.SendMessageAsync(string.Format(_l["administration_greetmsg_current_message", channel.Guild.Id], conf.ChannelGreetMessageText));
                    return;
                }
                await channel.SendMessageAsync(_l["administration_greetmsg_message_set", channel.Guild.Id]).ConfigureAwait(false);
                if (!conf.SendChannelGreetMessage)
                    await channel.SendMessageAsync(_l["administration_greetmsg_advanced_activation", channel.Guild.Id]).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task GreetDm(IUserMessage umsg)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    conf.SendDmGreetMessage = !conf.SendDmGreetMessage;
                    uow.GuildConfigs.Update(conf);
                    await uow.CompleteAsync();
                }

                if (conf.SendDmGreetMessage)
                    await channel.SendMessageAsync(_l["administration_greetdm_enabled", channel.Guild.Id]).ConfigureAwait(false);
                else
                    await channel.SendMessageAsync(_l["administration_greetdm_disabled", channel.Guild.Id]).ConfigureAwait(false);
            }
            
            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task GreetDmMsg(IUserMessage umsg, [Remainder] string text)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        conf.DmGreetMessageText = text;
                        uow.GuildConfigs.Update(conf);
                        await uow.CompleteAsync();
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    await channel.SendMessageAsync(string.Format(_l["administration_greetdmmsg_current_msg", channel.Guild.Id], conf.DmGreetMessageText));
                    return;
                }
                await channel.SendMessageAsync(_l["administration_greetdmmsg_message_set", channel.Guild.Id]).ConfigureAwait(false);
                if (!conf.SendDmGreetMessage)
                    await channel.SendMessageAsync(_l["administration_greetdmmsg_advanced_activation", channel.Guild.Id]).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task Bye(IUserMessage umsg)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    conf.SendChannelByeMessage = !conf.SendChannelByeMessage;
                    conf.ByeMessageChannelId = channel.Id;
                    uow.GuildConfigs.Update(conf);
                    await uow.CompleteAsync();
                }

                if (conf.SendChannelByeMessage)
                    await channel.SendMessageAsync(_l["administration_bye_enabled", channel.Guild.Id]).ConfigureAwait(false);
                else
                    await channel.SendMessageAsync(_l["administration_bye_disabled", channel.Guild.Id]).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task ByeMsg(IUserMessage umsg, [Remainder] string text)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        conf.ChannelByeMessageText = text;
                        uow.GuildConfigs.Update(conf);
                        await uow.CompleteAsync();
                    }
                }

                if (string.IsNullOrWhiteSpace(text))
                {
                    await channel.SendMessageAsync(string.Format(_l["administration_bye_message_current_msg", channel.Guild.Id], conf.ChannelGreetMessageText));
                    return;
                }
                await channel.SendMessageAsync(_l["administration_byemsg_message_set", channel.Guild.Id]).ConfigureAwait(false);
                if (!conf.SendChannelByeMessage)
                    await channel.SendMessageAsync(_l["administration_byemsg_advanced_activation", channel.Guild.Id]).ConfigureAwait(false);
            }

            [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
            [RequireContext(ContextType.Guild)]
            [RequirePermission(GuildPermission.ManageGuild)]
            public async Task ByeDel(IUserMessage umsg)
            {
                var channel = (ITextChannel)umsg.Channel;

                GuildConfig conf;
                using (var uow = DbHandler.UnitOfWork())
                {
                    conf = uow.GuildConfigs.For(channel.Guild.Id);
                    conf.AutoDeleteByeMessages = !conf.AutoDeleteByeMessages;
                    uow.GuildConfigs.Update(conf);
                    await uow.CompleteAsync();
                }

                if (conf.AutoDeleteByeMessages)
                    await channel.SendMessageAsync(_l["administration_byedel_enabled", channel.Guild.Id]).ConfigureAwait(false);
                else
                    await channel.SendMessageAsync(_l["administration_byedel_disabled", channel.Guild.Id]).ConfigureAwait(false);
            }

        }
    }
}