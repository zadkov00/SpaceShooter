using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace SpaceShooter
{
    public partial class Form1 : Form
    {
        // === GAME SETTINGS (made public) ===
        public const int PLAYER_SPEED = 5;
        public const int BULLET_SPEED = 10;
        public const int ENEMY_BASE_SPEED = 2;
        public const int GAME_WIDTH = 800;
        public const int GAME_HEIGHT = 600;

        // === GAME OBJECTS (made public static) ===
        public static Player player;
        public static List<Bullet> bullets;
        public static List<Enemy> enemies;
        public static List<PowerUp> powerUps;
        public static List<Particle> particles;
        public static Boss boss;

        // === GAME STATE (made public static) ===
        public static int score = 0;
        public static int level = 1;

        private Timer gameTimer;
        private int enemiesDestroyed = 0;
        private bool isGameOver = false;
        private bool isPaused = false;
        private Random random = new Random();

        // === CONTROLS ===
        private bool keyUp, keyDown, keyLeft, keyRight, keySpace;

        public Form1()
        {
            InitializeComponent();
            InitializeGame();
        }

        private void InitializeGame()
        {
            // Form setup
            this.DoubleBuffered = true;
            this.Size = new Size(GAME_WIDTH + 40, GAME_HEIGHT + 80);
            this.Text = "Space Shooter | Score: 0 | Lives: 3";
            this.BackColor = Color.FromArgb(10, 10, 30);
            this.KeyPreview = true;
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.StartPosition = FormStartPosition.CenterScreen;

            // Initialize objects
            player = new Player(GAME_WIDTH / 2, GAME_HEIGHT - 80);
            bullets = new List<Bullet>();
            enemies = new List<Enemy>();
            powerUps = new List<PowerUp>();
            particles = new List<Particle>();
            boss = null;

            // Game timer (60 FPS)
            gameTimer = new Timer();
            gameTimer.Interval = 16;
            gameTimer.Tick += GameLoop;
            gameTimer.Start();

            // Spawn first enemy wave
            SpawnEnemyWave();
        }

        private void SpawnEnemyWave()
        {
            int enemyCount = 5 + level * 2;

            for (int i = 0; i < enemyCount; i++)
            {
                int x = random.Next(50, GAME_WIDTH - 50);
                int y = random.Next(-500, -50);
                EnemyType type = (EnemyType)random.Next(0, 3);
                enemies.Add(new Enemy(x, y, type));
            }
        }

        private void SpawnBoss()
        {
            boss = new Boss(GAME_WIDTH / 2, -100);
        }

        private void CreateExplosion(int x, int y, Color color, int count = 20)
        {
            for (int i = 0; i < count; i++)
            {
                particles.Add(new Particle(x, y, color));
            }
        }

        private void GameLoop(object sender, EventArgs e)
        {
            if (isGameOver || isPaused) return;

            // === UPDATE PLAYER ===
            player.Update(keyUp, keyDown, keyLeft, keyRight, keySpace, GAME_WIDTH, GAME_HEIGHT);

            // === UPDATE BULLETS ===
            foreach (var bullet in bullets)
            {
                bullet.Update();
            }
            bullets.RemoveAll(b => b.Y < -20 || b.Y > GAME_HEIGHT + 20);

            // === UPDATE ENEMIES ===
            foreach (var enemy in enemies)
            {
                enemy.Update();
                // Enemy shoots
                if (random.Next(0, 100) < enemy.ShootChance)
                {
                    bullets.Add(new Bullet(enemy.X, enemy.Y + enemy.Height, 0, 5, false));
                }
            }

            // Remove enemies off-screen
            enemies.RemoveAll(enemy => enemy.Y > GAME_HEIGHT + 50);

            // === UPDATE BOSS ===
            if (boss != null)
            {
                boss.Update(GAME_WIDTH);
                // Boss shoots
                if (random.Next(0, 100) < 5)
                {
                    bullets.Add(new Bullet(boss.X, boss.Y + boss.Height, 0, 7, false));
                }
            }

            // === UPDATE POWER-UPS ===
            foreach (var powerUp in powerUps)
            {
                powerUp.Update();
            }
            powerUps.RemoveAll(p => p.Y > GAME_HEIGHT + 50);

            // === UPDATE PARTICLES ===
            foreach (var particle in particles)
            {
                particle.Update();
            }
            particles.RemoveAll(p => p.Life <= 0);

            // === CHECK COLLISIONS ===
            CheckCollisions();

            // === CHECK WAVES ===
            if (enemies.Count == 0 && boss == null)
            {
                enemiesDestroyed = 0;
                if (level % 3 == 0)
                {
                    SpawnBoss();
                }
                else
                {
                    level++;
                    SpawnEnemyWave();
                }
            }

            // === RENDERING ===
            this.Invalidate();

            // === UPDATE TITLE ===
            this.Text = $"Space Shooter | Level: {level} | Score: {score} | Lives: {player.Lives}";
        }

        private void CheckCollisions()
        {
            // Bullets vs Enemies
            foreach (var bullet in bullets.FindAll(b => b.IsPlayerBullet))
            {
                // Hit regular enemies
                // Fixed: renamed variable from "e" to "enemyItem"
                foreach (var enemyItem in enemies)
                {
                    // Fixed: added .GetBounds()
                    if (CheckCollision(bullet.GetBounds(), enemyItem.GetBounds()))
                    {
                        enemyItem.Health -= bullet.Damage;
                        bullet.MarkForDeletion();
                        CreateExplosion(bullet.X, bullet.Y, Color.Yellow, 5);

                        if (enemyItem.Health <= 0)
                        {
                            // Fixed: enemyItem.color instead of enemyItem.Color
                            CreateExplosion(enemyItem.X + enemyItem.Width / 2, enemyItem.Y + enemyItem.Height / 2, enemyItem.color, 30);
                            score += enemyItem.ScoreValue;
                            enemiesDestroyed++;

                            // Chance to drop power-up
                            if (random.Next(0, 100) < 10)
                            {
                                powerUps.Add(new PowerUp(enemyItem.X, enemyItem.Y));
                            }
                        }
                        break;
                    }
                }

                // Hit boss
                if (boss != null && CheckCollision(bullet.GetBounds(), boss.GetBounds()))
                {
                    boss.Health -= bullet.Damage;
                    bullet.MarkForDeletion();
                    CreateExplosion(bullet.X, bullet.Y, Color.Orange, 10);

                    if (boss.Health <= 0)
                    {
                        CreateExplosion(boss.X + boss.Width / 2, boss.Y + boss.Height / 2, Color.Red, 100);
                        score += 1000;
                        boss = null;
                        level++;
                    }
                }
            }

            // Enemy bullets vs Player
            foreach (var bullet in bullets.FindAll(b => !b.IsPlayerBullet))
            {
                // Fixed: added .GetBounds()
                if (CheckCollision(bullet.GetBounds(), player.GetBounds()))
                {
                    bullet.MarkForDeletion();
                    player.TakeDamage(1);
                    CreateExplosion(player.X + player.Width / 2, player.Y + player.Height / 2, Color.Red, 20);

                    if (player.Lives <= 0)
                    {
                        GameOver();
                    }
                }
            }

            // Enemies vs Player
            foreach (var enemyItem in enemies)
            {
                // Fixed: added .GetBounds()
                if (CheckCollision(enemyItem.GetBounds(), player.GetBounds()))
                {
                    enemyItem.Health = 0;
                    player.TakeDamage(1);
                    // Fixed: enemyItem.color instead of enemyItem.Color
                    CreateExplosion(enemyItem.X + enemyItem.Width / 2, enemyItem.Y + enemyItem.Height / 2, enemyItem.color, 30);

                    if (player.Lives <= 0)
                    {
                        GameOver();
                    }
                }
            }

            // Power-ups vs Player
            foreach (var powerUpItem in powerUps)
            {
                // Fixed: added .GetBounds()
                if (CheckCollision(powerUpItem.GetBounds(), player.GetBounds()))
                {
                    powerUpItem.Apply(player);
                    powerUpItem.MarkForDeletion();
                    score += 50;
                }
            }

            // Cleanup marked bullets
            bullets.RemoveAll(b => b.IsMarkedForDeletion);
            enemies.RemoveAll(e => e.Health <= 0);
            powerUps.RemoveAll(p => p.IsMarkedForDeletion);
        }

        private bool CheckCollision(RectangleF a, RectangleF b)
        {
            return a.IntersectsWith(b);
        }

        private void GameOver()
        {
            isGameOver = true;
            gameTimer.Stop();

            MessageBox.Show(
                $"GAME OVER!\n\nScore: {score}\nLevel: {level}\n\nPress OK to restart",
                "Game Over",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            Application.Restart();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W) keyUp = true;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S) keyDown = true;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) keyLeft = true;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) keyRight = true;
            if (e.KeyCode == Keys.Space) keySpace = true;
            if (e.KeyCode == Keys.P)
            {
                isPaused = !isPaused;
                this.Invalidate();
            }
            if (e.KeyCode == Keys.Escape && isGameOver) Application.Restart();
        }

        protected override void OnKeyUp(KeyEventArgs e)
        {
            base.OnKeyUp(e);

            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.W) keyUp = false;
            if (e.KeyCode == Keys.Down || e.KeyCode == Keys.S) keyDown = false;
            if (e.KeyCode == Keys.Left || e.KeyCode == Keys.A) keyLeft = false;
            if (e.KeyCode == Keys.Right || e.KeyCode == Keys.D) keyRight = false;
            if (e.KeyCode == Keys.Space) keySpace = false;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            Graphics g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            // Draw background stars
            DrawStars(g);
            // Draw player
            player.Draw(g);

            // Draw bullets
            foreach (var bullet in bullets)
            {
                bullet.Draw(g);
            }

            // Draw enemies
            foreach (var enemyItem in enemies)
            {
                enemyItem.Draw(g);
            }

            // Draw boss
            if (boss != null)
            {
                boss.Draw(g);
                // Boss health bar
                DrawBossHealthBar(g);
            }

            // Draw power-ups
            foreach (var powerUpItem in powerUps)
            {
                powerUpItem.Draw(g);
            }

            // Draw particles
            foreach (var particle in particles)
            {
                particle.Draw(g);
            }

            // Draw UI
            DrawUI(g);

            // Draw pause
            if (isPaused)
            {
                DrawPauseScreen(g);
            }
        }

        private void DrawStars(Graphics g)
        {
            // Simple starry background implementation
            Random starRandom = new Random(12345);
            for (int i = 0; i < 100; i++)
            {
                int x = starRandom.Next(0, GAME_WIDTH);
                int y = starRandom.Next(0, GAME_HEIGHT);
                int size = starRandom.Next(1, 3);
                g.FillRectangle(Brushes.White, x, y, size, size);
            }
        }

        private void DrawBossHealthBar(Graphics g)
        {
            int barWidth = 400;
            int barHeight = 20;
            int x = (GAME_WIDTH - barWidth) / 2;
            int y = 10;

            // Bar background
            g.FillRectangle(Brushes.DarkRed, x, y, barWidth, barHeight);

            // Health
            float healthPercent = (float)boss.Health / boss.MaxHealth;
            int healthWidth = (int)(barWidth * healthPercent);
            g.FillRectangle(Brushes.Red, x, y, healthWidth, barHeight);

            // Border
            g.DrawRectangle(Pens.White, x, y, barWidth, barHeight);
        }

        private void DrawUI(Graphics g)
        {
            // Score
            g.DrawString($"Score: {score}", new Font("Arial", 14, FontStyle.Bold), Brushes.White, 10, 10);

            // Lives
            for (int i = 0; i < player.Lives; i++)
            {
                g.FillRectangle(Brushes.Green, 10 + i * 25, GAME_HEIGHT - 30, 20, 20);
            }

            // Level
            g.DrawString($"Level: {level}", new Font("Arial", 14, FontStyle.Bold), Brushes.Yellow, GAME_WIDTH - 150, 10);
        }

        private void DrawPauseScreen(Graphics g)
        {
            // 1. Translucent dark background on the entire screen
            g.FillRectangle(new SolidBrush(Color.FromArgb(150, 0, 0, 0)), 0, 0, GAME_WIDTH, GAME_HEIGHT);

            // 2. Setting the text
            string pauseText = "Pause";
            string hint = "Press P to continue";

            // 3. Draw the word PAUSE (Large, in the center)
            using (Font font = new Font("Arial", 64, FontStyle.Bold))
            {
                // Measuring the text size for perfect centering
                SizeF textSize = g.MeasureString(pauseText, font);
                float x = (GAME_WIDTH - textSize.Width) / 2;
                float y = (GAME_HEIGHT - textSize.Height) / 2 - 30;

                // Text shadow (black)
                g.DrawString(pauseText, font, Brushes.Black, x + 3, y + 3);
                // Body text (white)
                g.DrawString(pauseText, font, Brushes.White, x, y);
            }

            // 4. Draw a hint (Finely, under the title)
            using (Font font = new Font("Arial", 18, FontStyle.Regular))
            {
                SizeF textSize = g.MeasureString(hint, font);
                float x = (GAME_WIDTH - textSize.Width) / 2;
                float y = GAME_HEIGHT / 2 + 40;

                g.DrawString(hint, font, Brushes.Yellow, x, y);
            }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            gameTimer?.Stop();
            base.OnFormClosing(e);
        }
    }

    // === PLAYER CLASS ===
    public class Player
    {
        public int X, Y, Width, Height;
        public int Lives = 3;
        public int FireRate = 15;
        private int fireCooldown = 0;

        public Player(int x, int y)
        {
            X = x;
            Y = y;
            Width = 40;
            Height = 40;
        }

        public void Update(bool up, bool down, bool left, bool right, bool space, int gameWidth, int gameHeight)
        {
            // Movement
            if (up && Y > 0) Y -= Form1.PLAYER_SPEED;
            if (down && Y < gameHeight - Height) Y += Form1.PLAYER_SPEED;
            if (left && X > 0) X -= Form1.PLAYER_SPEED;
            if (right && X < gameWidth - Width) X += Form1.PLAYER_SPEED;

            // Shooting
            if (space && fireCooldown <= 0)
            {
                Form1.bullets.Add(new Bullet(X + Width / 2 - 2, Y, 0, -Form1.BULLET_SPEED, true));
                fireCooldown = FireRate;
            }

            if (fireCooldown > 0) fireCooldown--;
        }

        public void TakeDamage(int damage)
        {
            Lives -= damage;
        }

        public void Draw(Graphics g)
        {
            // Draw ship (triangle)
            Point[] ship = new Point[]
            {
                new Point(X + Width / 2, Y),
                new Point(X, Y + Height),
                new Point(X + Width, Y + Height)
            };
            g.FillPolygon(Brushes.Cyan, ship);
            g.DrawPolygon(Pens.White, ship);
            // Engine
            g.FillRectangle(Brushes.Orange, X + Width / 2 - 5, Y + Height, 10, 15);
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(X, Y, Width, Height);
        }
    }

    // === BULLET CLASS ===
    public class Bullet
    {
        public int X, Y, Width, Height;
        public int SpeedX, SpeedY;
        public int Damage = 1;
        public bool IsPlayerBullet;
        public bool IsMarkedForDeletion = false;

        public Bullet(int x, int y, int speedX, int speedY, bool isPlayerBullet)
        {
            X = x;
            Y = y;
            Width = 4;
            Height = 15;
            SpeedX = speedX;
            SpeedY = speedY;
            IsPlayerBullet = isPlayerBullet;
        }

        public void Update()
        {
            X += SpeedX;
            Y += SpeedY;
        }

        public void MarkForDeletion()
        {
            IsMarkedForDeletion = true;
        }

        public void Draw(Graphics g)
        {
            g.FillRectangle(IsPlayerBullet ? Brushes.Lime : Brushes.Red, X, Y, Width, Height);
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(X, Y, Width, Height);
        }
    }

    // === ENEMY TYPES ===
    public enum EnemyType { Basic, Fast, Tank }

    // === ENEMY CLASS ===
    public class Enemy
    {
        public int X, Y, Width, Height;
        public int Health;
        public int Speed;
        public int ShootChance;
        public int ScoreValue;
        public Color color;  // renamed from Color (type conflict)
        public EnemyType Type;

        public Enemy(int x, int y, EnemyType type)
        {
            X = x;
            Y = y;
            Type = type;

            switch (type)
            {
                case EnemyType.Basic:
                    Width = 30;
                    Height = 30;
                    Health = 1;
                    Speed = 2;  // Form1.ENEMY_BASE_SPEED
                    ShootChance = 1;
                    ScoreValue = 100;
                    color = Color.Orange;
                    break;
                case EnemyType.Fast:
                    Width = 25;
                    Height = 25;
                    Health = 1;
                    Speed = 4;  // Form1.ENEMY_BASE_SPEED * 2
                    ShootChance = 2;
                    ScoreValue = 150;
                    color = Color.Yellow;
                    break;
                case EnemyType.Tank:
                    Width = 40;
                    Height = 40;
                    Health = 5;
                    Speed = 1;  // Form1.ENEMY_BASE_SPEED / 2
                    ShootChance = 3;
                    ScoreValue = 300;
                    color = Color.Purple;
                    break;
            }
        }

        public void Update()
        {
            Y += Speed;
            X += (int)Math.Sin(Y * 0.05) * 2;
        }

        public void Draw(Graphics g)
        {
            g.FillRectangle(new SolidBrush(color), X, Y, Width, Height);
            g.DrawRectangle(Pens.White, X, Y, Width, Height);
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(X, Y, Width, Height);
        }
    }

    // === BOSS CLASS ===
    public class Boss
    {
        public int X, Y, Width, Height;
        public int Health, MaxHealth;
        public int Speed;

        public Boss(int x, int y)
        {
            X = x;
            Y = y;
            Width = 100;
            Height = 80;
            MaxHealth = 50 + (Form1.level * 10);
            Health = MaxHealth;
            Speed = 3;
        }

        public void Update(int gameWidth)
        {
            // Move down to position
            if (Y < 50)
            {
                Y += Speed;
            }
            else
            {
                // Move side to side
                X += Speed;
                if (X <= 0 || X >= gameWidth - Width)
                {
                    Speed = -Speed;
                }
            }
        }

        public void Draw(Graphics g)
        {
            // Boss body
            g.FillRectangle(Brushes.DarkRed, X, Y, Width, Height);
            g.DrawRectangle(Pens.Red, X, Y, Width, Height);
            // Eyes
            g.FillRectangle(Brushes.Yellow, X + 20, Y + 20, 20, 20);
            g.FillRectangle(Brushes.Yellow, X + 60, Y + 20, 20, 20);
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(X, Y, Width, Height);
        }
    }

    // === POWER-UP CLASS ===
    public class PowerUp
    {
        public int X, Y, Width, Height;
        public PowerUpType Type;
        public bool IsMarkedForDeletion = false;

        public PowerUp(int x, int y)
        {
            X = x;
            Y = y;
            Width = 25;
            Height = 25;
            Type = (PowerUpType)new Random().Next(0, 3);
        }

        public void Update()
        {
            Y += 3;
        }

        public void Apply(Player player)
        {
            switch (Type)
            {
                case PowerUpType.Health:
                    player.Lives++;
                    break;
                case PowerUpType.FastFire:
                    player.FireRate = Math.Max(5, player.FireRate - 3);
                    break;
                case PowerUpType.Score:
                    Form1.score += 500;
                    break;
            }
        }

        public void MarkForDeletion()
        {
            IsMarkedForDeletion = true;
        }

        public void Draw(Graphics g)
        {
            // Fixed switch expression to regular switch for C# 7.3
            Color brushColor;
            switch (Type)
            {
                case PowerUpType.Health:
                    brushColor = Color.Green;
                    break;
                case PowerUpType.FastFire:
                    brushColor = Color.Blue;
                    break;
                case PowerUpType.Score:
                    brushColor = Color.Gold;
                    break;
                default:
                    brushColor = Color.White;
                    break;
            }

            g.FillEllipse(new SolidBrush(brushColor), X, Y, Width, Height);
            g.DrawEllipse(Pens.White, X, Y, Width, Height);
        }

        public RectangleF GetBounds()
        {
            return new RectangleF(X, Y, Width, Height);
        }
    }

    public enum PowerUpType { Health, FastFire, Score }

    // === PARTICLE CLASS (EXPLOSIONS) ===
    public class Particle
    {
        public int X, Y;
        public int SpeedX, SpeedY;
        public int Life;
        public Color color;  // renamed from Color (type conflict)

        public Particle(int x, int y, Color color)
        {
            X = x;
            Y = y;
            this.color = color;
            Life = 30;
            SpeedX = new Random().Next(-5, 5);
            SpeedY = new Random().Next(-5, 5);
        }

        public void Update()
        {
            X += SpeedX;
            Y += SpeedY;
            Life--;
        }

        public void Draw(Graphics g)
        {
            int alpha = (int)(255 * (Life / 30.0));
            Color c = Color.FromArgb(alpha, color);  // now works
            g.FillRectangle(new SolidBrush(c), X, Y, 4, 4);
        }
    }
}