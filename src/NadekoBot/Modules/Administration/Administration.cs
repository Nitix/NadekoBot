using Discord;
using Discord.Commands;
using NadekoBot.Extensions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NadekoBot.Services;
using NadekoBot.Attributes;
using System.Text.RegularExpressions;
using Discord.WebSocket;
using NadekoBot.Services.Database;
using NadekoBot.Services.Database.Models;

namespace NadekoBot.Modules.Administration
{
    [NadekoModule("Administration", ".")]
    public partial class Administration : DiscordModule
    {
        public Administration(ILocalization loc, CommandService cmds, DiscordSocketClient client) : base(loc, cmds, client)
        {
            NadekoBot.CommandHandler.CommandExecuted += DelMsgOnCmd_Handler;
        }

        private async void DelMsgOnCmd_Handler(object sender, CommandExecutedEventArgs e)
        {
            try
            {
                var channel = e.Message.Channel as ITextChannel;
                if (channel == null)
                    return;

                bool shouldDelete;
                using (var uow = DbHandler.UnitOfWork())
                {
                    shouldDelete = uow.GuildConfigs.For(channel.Guild.Id).DeleteMessageOnCommand;
                }

                if (shouldDelete)
                    await e.Message.DeleteAsync();
            }
            catch (Exception ex)
            {
                _log.Warn(ex, "Delmsgoncmd errored...");
            }
        }

        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task Restart(IUserMessage umsg)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    await channel.SendMessageAsync("`Restarting in 2 seconds...`");
        //    await Task.Delay(2000);
        //    System.Diagnostics.Process.Start(System.Reflection.Assembly.GetEntryAssembly().Location);
        //    Environment.Exit(0);
        //}

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.Administrator)]
        public async Task Delmsgoncmd(IUserMessage umsg)
        {
            var channel = (ITextChannel)umsg.Channel;
            GuildConfig conf;
            using (var uow = DbHandler.UnitOfWork())
            {
                conf = uow.GuildConfigs.For(channel.Guild.Id);
                conf.DeleteMessageOnCommand = !conf.DeleteMessageOnCommand;
                uow.GuildConfigs.Update(conf);
                await uow.CompleteAsync();
            }
            if (conf.DeleteMessageOnCommand)
                await channel.SendMessageAsync(_l["administration_delmsgoncmd_now_deleting", channel.Guild.Id]);
            else
                await channel.SendMessageAsync(_l["administration_delmsgoncmd_stopped_deletion", channel.Guild.Id]);
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageRoles)]
        public async Task Setrole(IUserMessage umsg, IGuildUser usr, [Remainder] IRole role)
        {
            var channel = (ITextChannel)umsg.Channel;
            try
            {
                await usr.AddRolesAsync(role).ConfigureAwait(false);
                await channel.SendMessageAsync(string.Format(_l["administration_setrole_success", channel.Guild.Id], role.Name, usr.Username )).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                await channel.SendMessageAsync(_l["administration_setrole_error_bot_permission", channel.Guild.Id]).ConfigureAwait(false);
                Console.WriteLine(ex.ToString());
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageRoles)]
        public async Task Removerole(IUserMessage umsg, IGuildUser usr, [Remainder] IRole role)
        {
            var channel = (ITextChannel)umsg.Channel;
            try
            {
                await usr.RemoveRolesAsync(role).ConfigureAwait(false);
                await channel.SendMessageAsync(string.Format(_l["administration_removerole_success", channel.Guild.Id], role.Name, usr.Username)).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_removerole_error_bot_permission", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageRoles)]
        public async Task RenameRole(IUserMessage umsg, IRole roleToEdit, string newname)
        {
            var channel = (ITextChannel)umsg.Channel;
            try
            {
                if (roleToEdit.Position > (await channel.Guild.GetCurrentUserAsync().ConfigureAwait(false)).Roles.Max(r => r.Position))
                {
                    await channel.SendMessageAsync(_l["administration_renamerole_error_higher", channel.Guild.Id]).ConfigureAwait(false);
                    return;
                }
                await roleToEdit.ModifyAsync(g => g.Name = newname).ConfigureAwait(false);
                await channel.SendMessageAsync(_l["administration_renamerole_success", channel.Guild.Id]).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await channel.SendMessageAsync(_l["administration_renamerole_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageRoles)]
        public async Task RemoveAllRoles(IUserMessage umsg, [Remainder] IGuildUser user)
        {
            var channel = (ITextChannel)umsg.Channel;

            try
            {
                await user.RemoveRolesAsync(user.Roles).ConfigureAwait(false);
                await channel.SendMessageAsync(string.Format(_l["administration_removeallroles_success", channel.Guild.Id], user.Username)).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_removeallroles_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageRoles)]
        public async Task CreateRole(IUserMessage umsg, [Remainder] string roleName = null)
        {
            var channel = (ITextChannel)umsg.Channel;


            if (string.IsNullOrWhiteSpace(roleName))
                return;
            try
            {
                var r = await channel.Guild.CreateRoleAsync(roleName).ConfigureAwait(false);
                await channel.SendMessageAsync(string.Format(_l["administration_createrole_success", channel.Guild.Id], r.Name)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await channel.SendMessageAsync(_l["administration_createrole_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageRoles)]
        public async Task RoleColor(IUserMessage umsg, params string[] args)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (args.Count() != 2 && args.Count() != 4)
            {
                await channel.SendMessageAsync(_l["global_params_error", channel.Guild.Id]).ConfigureAwait(false);
                return;
            }
            var roleName = args[0].ToUpperInvariant();
            var role = channel.Guild.Roles.FirstOrDefault(r => r.Name.ToUpperInvariant() == roleName);

            if (role == null)
            {
                await channel.SendMessageAsync(_l["administration_rolecolor_not_existant", channel.Guild.Id]).ConfigureAwait(false);
                return;
            }
            try
            {
                var rgb = args.Count() == 4;
                var arg1 = args[1].Replace("#", "");

                var red = Convert.ToByte(rgb ? int.Parse(arg1) : Convert.ToInt32(arg1.Substring(0, 2), 16));
                var green = Convert.ToByte(rgb ? int.Parse(args[2]) : Convert.ToInt32(arg1.Substring(2, 2), 16));
                var blue = Convert.ToByte(rgb ? int.Parse(args[3]) : Convert.ToInt32(arg1.Substring(4, 2), 16));
                
                await role.ModifyAsync(r => r.Color = new Color(red, green, blue).RawValue).ConfigureAwait(false);
                await channel.SendMessageAsync(string.Format(_l["administration_rolecolor_success", channel.Guild.Id], role.Name)).ConfigureAwait(false);
            }
            catch (Exception)
            {
                await channel.SendMessageAsync(_l["administration_rolecolor_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.BanMembers)]
        public async Task Ban(IUserMessage umsg, IGuildUser user)
        {
            var channel = (ITextChannel)umsg.Channel;

            var msg = "";

            if (!string.IsNullOrWhiteSpace(msg))
            {
                await (await user.CreateDMChannelAsync()).SendMessageAsync(string.Format(_l["administration_ban_private_message", channel.Guild.Id], channel.Guild.Name, msg)).ConfigureAwait(false);
                await Task.Delay(2000).ConfigureAwait(false); // temp solution; give time for a message to be send, fu volt
            }
            try
            {
                await channel.Guild.AddBanAsync(user, 7).ConfigureAwait(false);

                await channel.SendMessageAsync(string.Format(_l["administration_ban_success", channel.Guild.Id], user.Username, user.Id)).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_ban_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.BanMembers)]
        public async Task Softban(IUserMessage umsg, IGuildUser user, [Remainder] string msg = null)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (!string.IsNullOrWhiteSpace(msg))
            {
                await user.SendMessageAsync(string.Format(_l["administration_softban_private_message", channel.Guild.Id], channel.Guild.Name, msg)).ConfigureAwait(false);
                await Task.Delay(2000).ConfigureAwait(false); // temp solution; give time for a message to be send, fu volt
            }
            try
            {
                await channel.Guild.AddBanAsync(user, 7).ConfigureAwait(false);
                await channel.Guild.RemoveBanAsync(user).ConfigureAwait(false);

                await channel.SendMessageAsync(string.Format(_l["administration_softban_success", channel.Guild.Id], user.Username, user.Id)).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_softban_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        public async Task Kick(IUserMessage umsg, IGuildUser user, [Remainder] string msg = null)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (user == null)
            {
                await channel.SendMessageAsync(_l["administration_kick_error_user_not_found", channel.Guild.Id]).ConfigureAwait(false);
                return;
            }
            if (!string.IsNullOrWhiteSpace(msg))
            {
                await user.SendMessageAsync(string.Format(_l["administration_kick_private_message", channel.Guild.Id], channel.Guild.Name, msg)).ConfigureAwait(false);
                await Task.Delay(2000).ConfigureAwait(false); // temp solution; give time for a message to be send, fu volt
            }
            try
            {
                await user.KickAsync().ConfigureAwait(false);
                await channel.SendMessageAsync(string.Format(_l["administration_kick_success", channel.Guild.Id], user.Username, user.Id)).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_kick_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.MuteMembers)]
        public async Task Mute(IUserMessage umsg, params IGuildUser[] users)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (!users.Any())
            {
                await channel.SendMessageAsync(_l["administration_mute_nobody", channel.Guild.Id]).ConfigureAwait(false);
                return;
            }
            try
            {
                foreach (var u in users)
                {
                    await u.ModifyAsync(usr => usr.Mute = true).ConfigureAwait(false);
                }
                await channel.SendMessageAsync(_l["administration_mute_success", channel.Guild.Id]).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_mute_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.MuteMembers)]
        public async Task Unmute(IUserMessage umsg, params IGuildUser[] users)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (!users.Any())
            {
                await channel.SendMessageAsync(_l["administration_unmute_nobody", channel.Guild.Id]).ConfigureAwait(false);
                return;
            }
            try
            {
                foreach (var u in users)
                {
                    await u.ModifyAsync(usr => usr.Mute = false).ConfigureAwait(false);
                }
                await channel.SendMessageAsync(_l["administration_unmute_success", channel.Guild.Id]).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_unmute_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.DeafenMembers)]
        public async Task Deafen(IUserMessage umsg, params IGuildUser[] users)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (!users.Any())
            {
                await channel.SendMessageAsync(_l["administration_deafen_nobody", channel.Guild.Id]).ConfigureAwait(false);
                return;
            }
            try
            {
                foreach (var u in users)
                {
                    await u.ModifyAsync(usr=>usr.Deaf = true).ConfigureAwait(false);
                }
                await channel.SendMessageAsync(_l["administration_deafen_success", channel.Guild.Id]).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_deafen_error", channel.Guild.Id]).ConfigureAwait(false);
            }

        }
        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.DeafenMembers)]
        public async Task UnDeafen(IUserMessage umsg, params IGuildUser[] users)
        {
            var channel = (ITextChannel)umsg.Channel;

            if (!users.Any())
            {
                await channel.SendMessageAsync(_l["administration_undeafen_nobody", channel.Guild.Id]).ConfigureAwait(false);
                return;
            }
            try
            {
                foreach (var u in users)
                {
                    await u.ModifyAsync(usr=> usr.Deaf = false).ConfigureAwait(false);
                }
                await channel.SendMessageAsync(_l["administration_undeafen_success", channel.Guild.Id]).ConfigureAwait(false);
            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_undeafen_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageChannels)]
        public async Task DelVoiChanl(IUserMessage umsg, [Remainder] IVoiceChannel voiceChannel)
        {
            var channel = (ITextChannel)umsg.Channel;
            await voiceChannel.DeleteAsync().ConfigureAwait(false);
            await umsg.Channel.SendMessageAsync(string.Format(_l["administration_delvoichanl_success", channel.Guild.Id], voiceChannel.Name)).ConfigureAwait(false);
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageChannels)]
        public async Task CreatVoiChanl(IUserMessage umsg, [Remainder] string channelName)
        {
            var channel = (ITextChannel)umsg.Channel;
            var ch = await channel.Guild.CreateVoiceChannelAsync(channelName).ConfigureAwait(false);
            await channel.SendMessageAsync(string.Format(_l["administration_creatvoichanl_success", channel.Guild.Id], ch.Name, ch.Id)).ConfigureAwait(false);
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageChannels)]
        public async Task DelTxtChanl(IUserMessage umsg, [Remainder] ITextChannel channel)
        {
            var uchannel = (ITextChannel)umsg.Channel;
            await channel.DeleteAsync().ConfigureAwait(false);
            await channel.SendMessageAsync(string.Format(_l["administration_deltxtchanl_success", uchannel.Guild.Id], channel.Name, channel.Id)).ConfigureAwait(false);
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageChannels)]
        public async Task CreaTxtChanl(IUserMessage umsg, [Remainder] string channelName)
        {
            var channel = (ITextChannel)umsg.Channel;
            var txtCh = await channel.Guild.CreateTextChannelAsync(channelName).ConfigureAwait(false);
            await channel.SendMessageAsync(string.Format(_l["administration_creatxtchanl_success", channel.Guild.Id],txtCh.Name, txtCh.Id)).ConfigureAwait(false);
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageChannels)]
        public async Task SetTopic(IUserMessage umsg, [Remainder] string topic = null)
        {
            var channel = (ITextChannel)umsg.Channel;
            topic = topic ?? "";
            await channel.ModifyAsync(c => c.Topic = topic);
            await channel.SendMessageAsync(_l["administration_settopic_success", channel.Guild.Id]).ConfigureAwait(false);

        }
        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.ManageChannels)]
        public async Task SetChanlName(IUserMessage umsg, [Remainder] string name)
        {
            var channel = (ITextChannel)umsg.Channel;
            await channel.ModifyAsync(c => c.Name = name).ConfigureAwait(false);
            await channel.SendMessageAsync(_l["administration_setchanlname_success", channel.Guild.Id]).ConfigureAwait(false);
        }


        //delets her own messages, no perm required
        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        public async Task Prune(IUserMessage umsg)
        {
            var channel = (ITextChannel)umsg.Channel;

            var user = await channel.Guild.GetCurrentUserAsync();
            
            var enumerable = (await umsg.Channel.GetMessagesAsync()).Where(x => x.Author.Id == user.Id);
            await umsg.Channel.DeleteMessagesAsync(enumerable).ConfigureAwait(false);
            var msg = await channel.SendMessageAsync(_l["administration_prune_success", channel.Guild.Id]).ConfigureAwait(false);
            await Task.Delay(2000);
            await umsg.Channel.DeleteMessagesAsync(new List<IMessage> { msg }).ConfigureAwait(false);
        }

        // prune x
        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(ChannelPermission.ManageMessages)]
        public async Task Prune(IUserMessage msg, int count)
        {
            var channel = (ITextChannel)msg.Channel;
            await (msg as IUserMessage).DeleteAsync();
            while (count > 0)
            {
                int limit = (count < 100) ? count : 100;
                var enumerable = (await msg.Channel.GetMessagesAsync(limit: limit));
                await msg.Channel.DeleteMessagesAsync(enumerable);
                await Task.Delay(1000); // there is a 1 per second per guild ratelimit for deletemessages
                if (enumerable.Count < limit) break;
                count -= limit;
            }
            var ok = await channel.SendMessageAsync(_l["administration_prune_success", channel.Guild.Id]).ConfigureAwait(false);
            await Task.Delay(2000);
            await msg.Channel.DeleteMessagesAsync(new List<IMessage> { ok }).ConfigureAwait(false);
        }

        //prune @user [x]
        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        public async Task Prune(IUserMessage msg, IGuildUser user, int count = 100)
        {
            var channel = (ITextChannel)msg.Channel;
            int limit = (count < 100) ? count : 100;
            var enumerable = (await msg.Channel.GetMessagesAsync(limit: limit)).Where(m => m.Author == user);
            await msg.Channel.DeleteMessagesAsync(enumerable);
            var ok = await channel.SendMessageAsync(_l["administration_prune_success", channel.Guild.Id]).ConfigureAwait(false);
            await Task.Delay(2000);
            await msg.Channel.DeleteMessagesAsync(new List<IMessage> { ok }).ConfigureAwait(false);
        }
        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task Die(IUserMessage umsg)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    await channel.SendMessageAsync("`Shutting down.`").ConfigureAwait(false);
        //    await Task.Delay(2000).ConfigureAwait(false);
        //    Environment.Exit(0);
        //}

        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task Setname(IUserMessage umsg, [Remainder] string newName = null)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //}

        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task NewAvatar(IUserMessage umsg, [Remainder] string img = null)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    if (string.IsNullOrWhiteSpace(img))
        //        return;
        //    // Gather user provided URL.
        //    var avatarAddress = img;
        //    var imageStream = await SearchHelper.GetResponseStreamAsync(avatarAddress).ConfigureAwait(false);
        //    var image = System.Drawing.Image.FromStream(imageStream);
        //    await client.CurrentUser.Edit("", avatar: image.ToStream()).ConfigureAwait(false);

        //    // Send confirm.
        //    await channel.SendMessageAsync("New avatar set.").ConfigureAwait(false);
        //}

        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task SetGame(IUserMessage umsg, [Remainder] string game = null)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    game = game ?? "";

        //    client.SetGame(set_game);
        //}

        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task Send(IUserMessage umsg, string where, [Remainder] string msg = null)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    if (string.IsNullOrWhiteSpace(msg))
        //        return;

        //    var ids = where.Split('|');
        //    if (ids.Length != 2)
        //        return;
        //    var sid = ulong.Parse(ids[0]);
        //    var server = NadekoBot.Client.Servers.Where(s => s.Id == sid).FirstOrDefault();

        //    if (server == null)
        //        return;

        //    if (ids[1].ToUpperInvariant().StartsWith("C:"))
        //    {
        //        var cid = ulong.Parse(ids[1].Substring(2));
        //        var channel = server.TextChannels.Where(c => c.Id == cid).FirstOrDefault();
        //        if (channel == null)
        //        {
        //            return;
        //        }
        //        await channel.SendMessageAsync(msg);
        //    }
        //    else if (ids[1].ToUpperInvariant().StartsWith("U:"))
        //    {
        //        var uid = ulong.Parse(ids[1].Substring(2));
        //        var user = server.Users.Where(u => u.Id == uid).FirstOrDefault();
        //        if (user == null)
        //        {
        //            return;
        //        }
        //        await user.SendMessageAsync(msg);
        //    }
        //    else
        //    {
        //        await channel.SendMessageAsync("`Invalid format.`");
        //    }
        //}

        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task Announce(IUserMessage umsg, [Remainder] string message)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    foreach (var ch in (await _client.GetGuildsAsync().ConfigureAwait(false)).Select(async g => await g.GetDefaultChannelAsync().ConfigureAwait(false)))
        //    {
        //        await channel.SendMessageAsync(message).ConfigureAwait(false);
        //    }

        //    await channel.SendMessageAsync(":ok:").ConfigureAwait(false);
        //}

        ////todo owner only
        //[LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        //[RequireContext(ContextType.Guild)]
        //public async Task SaveChat(IUserMessage umsg, int cnt)
        //{
        //    var channel = (ITextChannel)umsg.Channel;

        //    ulong? lastmsgId = null;
        //    var sb = new StringBuilder();
        //    var msgs = new List<IUserMessage>(cnt);
        //    while (cnt > 0)
        //    {
        //        var dlcnt = cnt < 100 ? cnt : 100;
        //        IReadOnlyCollection<IUserMessage> dledMsgs;
        //        if (lastmsgId == null)
        //            dledMsgs = await umsg.Channel.GetMessagesAsync(cnt).ConfigureAwait(false);
        //        else
        //            dledMsgs = await umsg.Channel.GetMessagesAsync(lastmsgId.Value, Direction.Before, dlcnt);

        //        if (!dledMsgs.Any())
        //            break;

        //        msgs.AddRange(dledMsgs);
        //        lastmsgId = msgs[msgs.Count - 1].Id;
        //        cnt -= 100;
        //    }
        //    var title = $"Chatlog-{channel.Guild.Name}/#{channel.Name}-{DateTime.Now}.txt";
        //    await (umsg.Author as IGuildUser).SendFileAsync(
        //        await JsonConvert.SerializeObject(new { Messages = msgs.Select(s => s.ToString()) }, Formatting.Indented).ToStream().ConfigureAwait(false),
        //        title, title).ConfigureAwait(false);
        //}


        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.MentionEveryone)]
        public async Task MentionRole(IUserMessage umsg, params IRole[] roles)
        {
            var channel = (ITextChannel)umsg.Channel;

            string send = string.Format(_l["administration_mentionrole_invokation", channel.Guild.Id], umsg.Author.Mention);
            foreach (var role in roles)
            { 
                send += $"\n`{role.Name}`\n";
                send += string.Join(", ", (await channel.Guild.GetUsersAsync()).Where(u => u.Roles.Contains(role)).Distinct().Select(u=>u.Mention));
            }

            while (send.Length > 2000)
            {
                var curstr = send.Substring(0, 2000);
                await channel.SendMessageAsync(curstr.Substring(0,
                        curstr.LastIndexOf(", ", StringComparison.Ordinal) + 1)).ConfigureAwait(false);
                send = curstr.Substring(curstr.LastIndexOf(", ", StringComparison.Ordinal) + 1) +
                       send.Substring(2000);
            }
            await channel.SendMessageAsync(send).ConfigureAwait(false);
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        public async Task Donators(IUserMessage umsg)
        {
            var channel = (ITextChannel)umsg.Channel;
            IEnumerable<Donator> donatorsOrdered;
            using (var uow = DbHandler.UnitOfWork())
            {
                donatorsOrdered = uow.Donators.GetDonatorsOrdered();
            }

            string str = _l["administration_donators_list", channel.Guild.Id];
            await channel.SendMessageAsync(str + string.Join("⭐", donatorsOrdered.Select(d => d.Name))).ConfigureAwait(false);
        }


        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        public async Task Donadd(IUserMessage umsg, IUser donator, int amount)
        {
            var channel = (ITextChannel)umsg.Channel;

            Donator don;
            using (var uow = DbHandler.UnitOfWork())
            {
                don = uow.Donators.AddOrUpdateDonator(donator.Id, donator.Username, amount);
                await uow.CompleteAsync();
            }

            await channel.SendMessageAsync(string.Format(_l["administration_donadd_success", channel.Guild.Id], don.Amount)).ConfigureAwait(false);
        }

        [LocalizedCommand, LocalizedDescription, LocalizedSummary, LocalizedAlias]
        [RequireContext(ContextType.Guild)]
        [RequirePermission(GuildPermission.Administrator)]
        public async Task SetLanguage(IUserMessage umsg, [Remainder] string language = "")
        {
            var channel = (ITextChannel) umsg.Channel;

            if (string.IsNullOrWhiteSpace(language))
            {
                await channel.SendMessageAsync(_l["administration_language_set_help", channel.Guild.Id]);
                return;
            }
            try
            {
                using (var uow = DbHandler.UnitOfWork())
                {
                    {
                        var test = new CultureInfo(language); //Just test, will throw if it's an incorrect one
                    }
                    var guild = uow.GuildConfigs.For(channel.Guild.Id);
                    guild.Language = language;
                    uow.GuildConfigs.Update(guild);
                    await uow.CompleteAsync();
                }
                await channel.SendMessageAsync(_l["administration_language_set_done", channel.Guild.Id]).ConfigureAwait(false);

            }
            catch
            {
                await channel.SendMessageAsync(_l["administration_language_set_error", channel.Guild.Id]).ConfigureAwait(false);
            }
        }
    }
}
