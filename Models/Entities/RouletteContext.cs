using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RussianRouletteTGBot.Models.Entities;

public partial class RouletteContext : DbContext
{
    public RouletteContext()
    {
    }

    public RouletteContext(DbContextOptions<RouletteContext> options)
        : base(options)
    {
    }

    public virtual DbSet<BulletsInGame> BulletsInGames { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<MoneyBonuse> MoneyBonuses { get; set; }

    public virtual DbSet<ResultsOfGame> ResultsOfGames { get; set; }

    public virtual DbSet<Setting> Settings { get; set; }

    public virtual DbSet<TypesOfBullet> TypesOfBullets { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<BulletsInGame>(entity =>
        {
            entity.HasKey(e => e.IdBulletInGame).HasName("bullets_in_game_pkey");

            entity.ToTable("bullets_in_game");

            entity.Property(e => e.IdBulletInGame).HasColumnName("id_bullet_in_game");
            entity.Property(e => e.GameId).HasColumnName("game_id");
            entity.Property(e => e.IndexOfBullet).HasColumnName("index_of_bullet");

            entity.HasOne(d => d.Game).WithMany(p => p.BulletsInGames)
                .HasForeignKey(d => d.GameId)
                .HasConstraintName("bullets_in_game_game_id_fkey");
        });

        modelBuilder.Entity<Game>(entity =>
        {
            entity.HasKey(e => e.IdGame).HasName("games_pkey");

            entity.ToTable("games");

            entity.Property(e => e.IdGame).HasColumnName("id_game");
            entity.Property(e => e.Bet).HasColumnName("bet");
            entity.Property(e => e.CountOfRounds)
                .HasDefaultValue((short)1)
                .HasColumnName("count_of_rounds");
            entity.Property(e => e.ResultId).HasColumnName("result_id");
            entity.Property(e => e.SettingsId).HasColumnName("settings_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Winning)
                .HasDefaultValue(0)
                .HasColumnName("winning");

            entity.HasOne(d => d.Result).WithMany(p => p.Games)
                .HasForeignKey(d => d.ResultId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("games_result_id_fkey");

            entity.HasOne(d => d.Settings).WithMany(p => p.Games)
                .HasForeignKey(d => d.SettingsId)
                .HasConstraintName("games_settings_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Games)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("games_user_id_fkey");
        });

        modelBuilder.Entity<MoneyBonuse>(entity =>
        {
            entity.HasKey(e => e.IdMoneyBonus).HasName("money_bonuses_pkey");

            entity.ToTable("money_bonuses");

            entity.Property(e => e.IdMoneyBonus).HasColumnName("id_money_bonus");
            entity.Property(e => e.CollectionTime)
                .HasDefaultValueSql("CURRENT_TIMESTAMP")
                .HasColumnType("timestamp without time zone")
                .HasColumnName("collection_time");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.MoneyBonuses)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("money_bonuses_user_id_fkey");
        });

        modelBuilder.Entity<ResultsOfGame>(entity =>
        {
            entity.HasKey(e => e.IdResultOfGame).HasName("results_of_game_pkey");

            entity.ToTable("results_of_game");

            entity.Property(e => e.IdResultOfGame).HasColumnName("id_result_of_game");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
        });

        modelBuilder.Entity<Setting>(entity =>
        {
            entity.HasKey(e => e.IdSetting).HasName("settings_pkey");

            entity.ToTable("settings");

            entity.Property(e => e.IdSetting).HasColumnName("id_setting");
            entity.Property(e => e.CountOfBullets)
                .HasDefaultValue((short)1)
                .HasColumnName("count_of_bullets");
            entity.Property(e => e.TypeOfBulletId)
                .HasDefaultValue(1)
                .HasColumnName("type_of_bullet_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.TypeOfBullet).WithMany(p => p.Settings)
                .HasForeignKey(d => d.TypeOfBulletId)
                .HasConstraintName("settings_type_of_bullet_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Settings)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("settings_user_id_fkey");
        });

        modelBuilder.Entity<TypesOfBullet>(entity =>
        {
            entity.HasKey(e => e.IdTypeOfBullet).HasName("types_of_bullet_pkey");

            entity.ToTable("types_of_bullet");

            entity.Property(e => e.IdTypeOfBullet).HasColumnName("id_type_of_bullet");
            entity.Property(e => e.Multiplier)
                .HasPrecision(3, 2)
                .HasColumnName("multiplier");
            entity.Property(e => e.Price).HasColumnName("price");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.IdUser).HasName("users_pkey");

            entity.ToTable("users");

            entity.Property(e => e.IdUser).HasColumnName("id_user");
            entity.Property(e => e.BotStateId)
                .HasDefaultValue(1)
                .HasColumnName("bot_state_id");
            entity.Property(e => e.FirstName)
                .HasMaxLength(255)
                .HasColumnName("first_name");
            entity.Property(e => e.MaxScore)
                .HasDefaultValue(0)
                .HasColumnName("max_score");
            entity.Property(e => e.Score)
                .HasDefaultValue(0)
                .HasColumnName("score");
            entity.Property(e => e.TgId).HasColumnName("tg_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
