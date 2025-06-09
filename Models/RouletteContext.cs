using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace RussianRouletteTGBot.Models;

public partial class RouletteContext : DbContext
{
    public RouletteContext()
    {
    }

    public RouletteContext(DbContextOptions<RouletteContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Achievement> Achievements { get; set; }

    public virtual DbSet<BulletsInGame> BulletsInGames { get; set; }

    public virtual DbSet<Game> Games { get; set; }

    public virtual DbSet<ResultsOfGame> ResultsOfGames { get; set; }

    public virtual DbSet<TypesOfBullet> TypesOfBullets { get; set; }

    public virtual DbSet<User> Users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseNpgsql("Host=localhost;Port=5432;Database=roulette;Username=admin;Password=admin");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Achievement>(entity =>
        {
            entity.HasKey(e => e.IdAchivevement).HasName("achievements_pkey");

            entity.ToTable("achievements");

            entity.Property(e => e.IdAchivevement).HasColumnName("id_achivevement");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
        });

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
                .HasDefaultValue((short)0)
                .HasColumnName("count_of_rounds");
            entity.Property(e => e.ResultId).HasColumnName("result_id");
            entity.Property(e => e.TypeOfBulletId)
                .HasDefaultValue(1)
                .HasColumnName("type_of_bullet_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.Winning)
                .HasDefaultValue(0)
                .HasColumnName("winning");

            entity.HasOne(d => d.Result).WithMany(p => p.Games)
                .HasForeignKey(d => d.ResultId)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("games_result_id_fkey");

            entity.HasOne(d => d.TypeOfBullet).WithMany(p => p.Games)
                .HasForeignKey(d => d.TypeOfBulletId)
                .HasConstraintName("games_type_of_bullet_id_fkey");

            entity.HasOne(d => d.User).WithMany(p => p.Games)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("games_user_id_fkey");
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
            entity.Property(e => e.TgId).HasColumnName("tg_id");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
