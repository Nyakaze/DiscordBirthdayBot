
using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using SadBot.Data;

#nullable disable

namespace SadBot.Data.Migrations
{
    [DbContext(typeof(SadBotDbContext))]
    partial class SadBotDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
#pragma warning disable 612, 618
            modelBuilder
                .HasAnnotation("ProductVersion", "10.0.4")
                .HasAnnotation("Relational:MaxIdentifierLength", 63);

            NpgsqlModelBuilderExtensions.UseIdentityByDefaultColumns(modelBuilder);

            modelBuilder.Entity("SadBot.Data.Birthday", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<long>("AddedBy")
                        .HasColumnType("bigint");

                    b.Property<int>("Day")
                        .HasColumnType("integer");

                    b.Property<long>("GuildId")
                        .HasColumnType("bigint");

                    b.Property<int>("Month")
                        .HasColumnType("integer");

                    b.Property<string>("TimeZoneId")
                        .IsRequired()
                        .HasMaxLength(64)
                        .HasColumnType("character varying(64)");

                    b.Property<DateTimeOffset>("UpdatedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.Property<int?>("Year")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("Month");

                    b.HasIndex("GuildId", "UserId")
                        .IsUnique();

                    b.ToTable("Birthdays");
                });

            modelBuilder.Entity("SadBot.Data.BirthdaySettings", b =>
                {
                    b.Property<long>("GuildId")
                        .HasColumnType("bigint");

                    b.Property<long?>("AnnouncementChannelId")
                        .HasColumnType("bigint");

                    b.Property<long?>("BirthdayRoleId")
                        .HasColumnType("bigint");

                    b.Property<string>("MessageTemplate")
                        .HasMaxLength(1500)
                        .HasColumnType("character varying(1500)");

                    b.HasKey("GuildId");

                    b.ToTable("BirthdaySettings");
                });

            modelBuilder.Entity("SadBot.Data.BoardMessage", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<long>("ChannelId")
                        .HasColumnType("bigint");

                    b.Property<long>("GuildId")
                        .HasColumnType("bigint");

                    b.Property<long>("MessageId")
                        .HasColumnType("bigint");

                    b.Property<int>("Page")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("GuildId", "Page")
                        .IsUnique();

                    b.ToTable("BoardMessages");
                });

            modelBuilder.Entity("SadBot.Data.Celebration", b =>
                {
                    b.Property<int>("Id")
                        .ValueGeneratedOnAdd()
                        .HasColumnType("integer");

                    NpgsqlPropertyBuilderExtensions.UseIdentityByDefaultColumn(b.Property<int>("Id"));

                    b.Property<DateTimeOffset>("CelebratedAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long?>("ChannelId")
                        .HasColumnType("bigint");

                    b.Property<bool>("Expired")
                        .HasColumnType("boolean");

                    b.Property<DateTimeOffset>("ExpiresAt")
                        .HasColumnType("timestamp with time zone");

                    b.Property<long>("GuildId")
                        .HasColumnType("bigint");

                    b.Property<long?>("MessageId")
                        .HasColumnType("bigint");

                    b.Property<long?>("RoleId")
                        .HasColumnType("bigint");

                    b.Property<long>("UserId")
                        .HasColumnType("bigint");

                    b.Property<int>("Year")
                        .HasColumnType("integer");

                    b.HasKey("Id");

                    b.HasIndex("Expired", "ExpiresAt");

                    b.HasIndex("GuildId", "UserId", "Year")
                        .IsUnique();

                    b.ToTable("Celebrations");
                });
#pragma warning restore 612, 618
        }
    }
}
