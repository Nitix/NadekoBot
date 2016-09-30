﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NadekoBot.Services.Database.Models
{
    public class Reminder : DbEntity
    {
        public DateTime When { get; set; }
        /// <summary>
        /// <strong>DO NOT USE IT DIRECTLY</strong>, pls use <see cref="ChannelId"/>.
        /// It's used internally by EF
        /// </summary>
        [Column("ChannelId")]
        public long _channelId { get; set; }

        [NotMapped]
        public ulong ChannelId
        {
            get { return Convert.ToUInt64(_channelId); }
            set { _channelId = Convert.ToInt64(value); }
        }

        /// <summary>
        /// <strong>DO NOT USE IT DIRECTLY</strong>, pls use <see cref="GuildId"/>.
        /// It's used internally by EF
        /// </summary>
        [Column("GuildId")]
        public long _guildId { get; set; }

        [NotMapped]
        public ulong GuildId
        {
            get { return Convert.ToUInt64(_guildId); }
            set { _guildId = Convert.ToInt64(value); }
        }

        /// <summary>
        /// <strong>DO NOT USE IT DIRECTLY</strong>, pls use <see cref="UserId"/>.
        /// It's used internally by EF
        /// </summary>
        [Column("UserId")]
        public long _userId { get; set; }

        [NotMapped]
        public ulong UserId
        {
            get { return Convert.ToUInt64(_userId); }
            set { _userId = Convert.ToInt64(value); }
        }
        public string Message { get; set; }
        public bool IsPrivate { get; set; }
    }
}
