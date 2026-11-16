using System.Text.Json;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using MoonDropMeadows.Core;
using MoonDropMeadows.UI;

namespace MoonDropMeadows;

public sealed class Game1 : Game
{
    private readonly GraphicsDeviceManager graphics;
    private SpriteBatch batch = null!;
    private Texture2D pixel = null!;
    private readonly Random random = new();
    private GameSave save = new();
    private Vector2 player = new(480, 330);
    private readonly Dictionary<string, Rectangle> places = new();
    private string panelTitle = "";
    private string[] panelLines = Array.Empty<string>();
    private Action<int>? panelChoice;
    private int panelIndex;
    private bool characterSetup = true;
    private bool editingName;
    private string nameBuffer = "Rowan";
    private int ageChoice = 18;
    private bool siblingChoice = true;
    private KeyboardState previous;
    private double clock;
    private double danceClock;
    private bool dancing;
    private string toast = "WELCOME TO MOONDROP MEADOWS";
    private double toastTimer = 5;
    private string currentSeason => new[] { "SPRING", "SUMMER", "AUTUMN", "WINTER" }[(save.DayOfYear - 1) / 28 % 4];
    private string timeText => $"{save.TimeMinutes / 60:00}:{save.TimeMinutes % 60:00}";

    public Game1()
    {
        graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.Title = "MoonDrop Meadows";
        graphics.PreferredBackBufferWidth = 960;
        graphics.PreferredBackBufferHeight = 640;
        graphics.IsFullScreen = false;
    }

    protected override void Initialize()
    {
        places["HOME"] = new Rectangle(80, 100, 150, 100);
        places["SCHOOL"] = new Rectangle(390, 55, 170, 100);
        places["GENERAL STORE"] = new Rectangle(690, 95, 150, 90);
        places["CLOTHING"] = new Rectangle(700, 225, 145, 90);
        places["CAFE"] = new Rectangle(385, 245, 150, 90);
        places["MAYOR'S OFFICE"] = new Rectangle(90, 420, 160, 95);
        places["GAZETTE"] = new Rectangle(385, 440, 130, 70);
        places["BUSINESS CENTER"] = new Rectangle(675, 420, 175, 95);
        places["BOB'S TOWER"] = new Rectangle(70, 260, 145, 110);
        base.Initialize();
    }

    protected override void LoadContent()
    {
        batch = new SpriteBatch(GraphicsDevice);
        pixel = new Texture2D(GraphicsDevice, 1, 1);
        pixel.SetData(new[] { Color.White });
        LoadSave();
    }

    protected override void Update(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (toastTimer > 0) toastTimer -= dt;
        if (panelTitle.Length > 0) UpdatePanel(keyboard);
        else if (characterSetup) UpdateCreation(keyboard);
        else
        {
            clock += dt;
            if (clock >= 2.5)
            {
                clock = 0;
                save.TimeMinutes += 10;
                if (save.TimeMinutes >= 24 * 60) NextDay();
            }
            UpdateMovement(keyboard, dt);
            if (Pressed(keyboard, Keys.E)) Interact();
            if (Pressed(keyboard, Keys.F)) Farm();
            if (Pressed(keyboard, Keys.C)) OpenComputer();
            if (Pressed(keyboard, Keys.G)) GatherPetal();
            if (Pressed(keyboard, Keys.F5)) { SaveGame(); Notify("GAME SAVED"); }
            if (Pressed(keyboard, Keys.Escape)) OpenPanel("PAUSED", new[] { "RESUME", "SAVE GAME", "VOICE SETTINGS", "RETURN TO TITLE" }, i =>
            {
                if (i == 1) { SaveGame(); Notify("GAME SAVED"); }
                if (i == 2) { save.VoiceActingEnabled = !save.VoiceActingEnabled; Notify(save.VoiceActingEnabled ? "VOICE MODE ON" : "VOICE MODE OFF"); }
                if (i == 3) characterSetup = true;
            });
        }
        previous = keyboard;
        base.Update(gameTime);
    }

    private bool Pressed(KeyboardState now, Keys key) => now.IsKeyDown(key) && !previous.IsKeyDown(key);

    private void UpdateCreation(KeyboardState k)
    {
        if (editingName)
        {
            foreach (var key in k.GetPressedKeys())
            {
                if (previous.IsKeyDown(key)) continue;
                if (key == Keys.Enter) editingName = false;
                else if (key == Keys.Back && nameBuffer.Length > 0) nameBuffer = nameBuffer[..^1];
                else if (key >= Keys.A && key <= Keys.Z && nameBuffer.Length < 14) nameBuffer += key.ToString().ToLowerInvariant();
                else if (key >= Keys.D0 && key <= Keys.D9 && nameBuffer.Length < 14) nameBuffer += key.ToString()[1];
                else if (key == Keys.Space && nameBuffer.Length < 14) nameBuffer += " ";
            }
        }
        else
        {
            if (Pressed(k, Keys.Up)) ageChoice = Math.Min(80, ageChoice + 1);
            if (Pressed(k, Keys.Down)) ageChoice = Math.Max(8, ageChoice - 1);
            if (Pressed(k, Keys.F)) siblingChoice = !siblingChoice;
            if (Pressed(k, Keys.N)) editingName = true;
            if (Pressed(k, Keys.Enter))
            {
                save.Character.Name = string.IsNullOrWhiteSpace(nameBuffer) ? "Rowan" : nameBuffer.Trim();
                save.Character.Age = ageChoice;
                save.Character.HasSibling = siblingChoice;
                characterSetup = false;
                Notify(ageChoice < 13 ? "FIRST DAY OF A NEW ADVENTURE!" : "A NEW CHAPTER BEGINS!");
            }
        }
    }

    private void UpdateMovement(KeyboardState k, float dt)
    {
        Vector2 move = Vector2.Zero;
        if (k.IsKeyDown(Keys.W) || k.IsKeyDown(Keys.Up)) move.Y--;
        if (k.IsKeyDown(Keys.S) || k.IsKeyDown(Keys.Down)) move.Y++;
        if (k.IsKeyDown(Keys.A) || k.IsKeyDown(Keys.Left)) move.X--;
        if (k.IsKeyDown(Keys.D) || k.IsKeyDown(Keys.Right)) move.X++;
        if (move != Vector2.Zero) move.Normalize();
        player += move * 150f * dt;
        player.X = Math.Clamp(player.X, 24, 936);
        player.Y = Math.Clamp(player.Y, 70, 590);
    }

    private void UpdatePanel(KeyboardState k)
    {
        if (Pressed(k, Keys.Escape)) { ClosePanel(); return; }
        if (Pressed(k, Keys.Up)) panelIndex = (panelIndex - 1 + panelLines.Length) % panelLines.Length;
        if (Pressed(k, Keys.Down)) panelIndex = (panelIndex + 1) % panelLines.Length;
        for (int i = 0; i < Math.Min(9, panelLines.Length); i++)
            if (Pressed(k, (Keys)((int)Keys.D1 + i))) { Choose(i); return; }
        if (Pressed(k, Keys.Enter)) Choose(panelIndex);
    }

    private void Choose(int i)
    {
        if (i < 0 || i >= panelLines.Length) return;
        var action = panelChoice;
        ClosePanel();
        action?.Invoke(i);
    }

    private void ClosePanel() { panelTitle = ""; panelLines = Array.Empty<string>(); panelChoice = null; panelIndex = 0; }
    private void OpenPanel(string title, string[] lines, Action<int>? action = null)
    {
        panelTitle = title; panelLines = lines; panelChoice = action; panelIndex = 0;
    }
    private void Notify(string text) { toast = text; toastTimer = 3.2; }

    private void Interact()
    {
        var nearby = places.OrderBy(p => Vector2.Distance(player, new Vector2(p.Value.Center.X, p.Value.Center.Y))).FirstOrDefault();
        if (nearby.Key == null || Vector2.Distance(player, new Vector2(nearby.Value.Center.X, nearby.Value.Center.Y)) > 115)
        { TalkToResident(); return; }

        switch (nearby.Key)
        {
            case "HOME": OpenPanel("HOME SWEET HOME", new[] { "REST UNTIL TOMORROW", "CHECK FAMILY MAIL", "OPEN COMPUTER", "DECORATE HOME" }, i =>
            {
                if (i == 0) { save.Character.Energy = 100; save.TimeMinutes = 7 * 60; NextDay(); }
                if (i == 1) ShowMail();
                if (i == 2) OpenComputer();
                if (i == 3) Notify("HOME DECORATING ARRIVES IN A FUTURE UPDATE");
            }); break;
            case "SCHOOL":
                if (save.Character.Age < 18) { save.Character.Energy = Math.Max(0, save.Character.Energy - 10); save.SchoolDays++; save.Character.Knowledge += 2; Notify("SCHOOL DAY COMPLETE! +2 KNOWLEDGE"); }
                else OpenPanel("MOONDROP LEARNING CENTER", new[] { "TAKE AN EVENING CLASS", "LIBRARY RESEARCH", "CLOSE" }, i => { if (i == 0 || i == 1) { save.Character.Knowledge += 2; Notify("YOU LEARNED SOMETHING NEW"); } });
                break;
            case "GENERAL STORE": OpenPanel("PIPER'S GENERAL STORE", new[] { "BUY SEEDS - 20", "BUY SNACK - 12", "SELL HARVEST - 35", "LEAVE" }, i =>
            {
                if (i == 0 && save.Character.Money >= 20) { save.Character.Money -= 20; save.Character.Seeds += 5; Notify("5 SEEDS ADDED"); }
                if (i == 1 && save.Character.Money >= 12) { save.Character.Money -= 12; save.Character.Energy = Math.Min(100, save.Character.Energy + 20); Notify("A DELICIOUS SNACK!"); }
                if (i == 2) { save.Character.Money += 35; Notify("SOLD SOME PRODUCE +35"); }
            }); break;
            case "CLOTHING": OpenPanel("THREADS & THIMBLE", new[] { "BUY FESTIVAL OUTFIT - 80", "BUY COZY OUTFIT - 60", "LEAVE" }, i =>
            {
                int cost = i == 0 ? 80 : 60;
                if (i < 2 && save.Character.Money >= cost) { save.Character.Money -= cost; save.Character.Outfit = i == 0 ? "Festival" : "Cozy"; Notify("NEW OUTFIT UNLOCKED!"); }
            }); break;
            case "CAFE": OpenPanel("THE LITTLE LANTERN CAFE", new[] { "HAVE TEA - 15", "MEET A FRIEND", "LEAVE" }, i =>
            {
                if (i == 0 && save.Character.Money >= 15) { save.Character.Money -= 15; save.Character.Energy = Math.Min(100, save.Character.Energy + 15); Notify("A WARM CUP OF TEA"); }
                if (i == 1) TalkToResident();
            }); break;
            case "MAYOR'S OFFICE": MayorScene(); break;
            case "GAZETTE": ReadPaper(); break;
            case "BUSINESS CENTER": BusinessScene(); break;
            case "BOB'S TOWER": BobScene(); break;
        }
    }

    private void TalkToResident()
    {
        string[] people = { "Juniper", "Nell", "Piper", "Theo", "Mina" };
        string person = people[random.Next(people.Length)];
        OpenPanel(person.ToUpperInvariant(), new[] { "ASK HOW THEY ARE", "GIVE A FLOWER", "INVITE TO A DANCE", "SAY GOODBYE" }, i =>
        {
            if (i == 0) { AddFriend(person, 3); Notify(person.ToUpperInvariant() + ": IT'S A LOVELY DAY, ISN'T IT?"); }
            if (i == 1 && save.GatheredItems.Contains("Wildflower")) { save.GatheredItems.Remove("Wildflower"); AddFriend(person, 12); Notify(person + " LOVED THE FLOWER!"); }
            if (i == 2) { AddFriend(person, 5); dancing = true; danceClock = 4; Notify("YOU DANCED UNDER THE STRING LIGHTS!"); }
        });
    }

    private void AddFriend(string id, int amount)
    {
        var r = save.Relationships.FirstOrDefault(x => x.CharacterId == id);
        if (r == null) { r = new RelationshipData { CharacterId = id }; save.Relationships.Add(r); }
        r.Affection = Math.Min(100, r.Affection + amount);
    }

    private void MayorScene()
    {
        if (save.StoryStep == 0)
            OpenPanel("MAYORLARRY", new[] { "HELLO, MAYORLARRY!", "ASK ABOUT THE BLUE LIGHT", "LEAVE" }, i =>
            {
                if (i == 0) { AddFriend("MAYORLARRY", 2); Notify("MAYORLARRY: WELCOME, WELCOME!"); }
                if (i == 1) { save.StoryStep = 1; Notify("MAYORLARRY: ASK BOB. AND PLEASE DON'T PANIC."); }
            });
        else
            OpenPanel("MAYORLARRY", new[] { "ASK ABOUT THE MOONWELL", "VOLUNTEER FOR THE TOWN", "LEAVE" }, i =>
            {
                if (i == 0) Notify("MAYORLARRY: THAT OLD STORY IS BEST HEARD FROM BOB.");
                if (i == 1) { save.Character.Money += 25; Notify("COMMUNITY ERRAND COMPLETE! +25"); }
            });
    }

    private void BobScene()
    {
        if (save.StoryStep < 2)
            OpenPanel("BOB THE WIZARD", new[] { "ASK ABOUT THE BLUE LIGHT", "OFFER TO HELP", "ASK ABOUT HIS HAT" }, i =>
            {
                if (i == 0) { save.StoryStep = 2; Notify("BOB: THE MOONWELL IS WAKING UP! FIND 3 MOON PETALS."); }
                if (i == 1) { save.StoryStep = 2; Notify("BOB: EXCELLENT! THREE MOON PETALS, PLEASE."); }
                if (i == 2) Notify("BOB: IT IS A VERY IMPORTANT HAT. IT IS ALSO A BOWL.");
            });
        else if (save.StoryStep == 2)
            OpenPanel("BOB'S MOON PETAL RESEARCH", new[] { "ASK WHAT THE PETALS DO", "CHECK YOUR PROGRESS", "LEAVE" }, i =>
            {
                if (i == 0) Notify("BOB: THEY HOLD MEMORIES. ALSO, THEY SMELL A BIT LIKE PEARS.");
                if (i == 1) Notify("MOON PETALS: " + save.MoonPetals + " / 3");
            });
        else
            OpenPanel("BOB THE WIZARD", new[] { "ASK ABOUT THE NEXT CLUE", "TELL HIM THE TOWN IS CHANGING" }, i => Notify("BOB: THE MOON REMEMBERS EVERYONE WHO EVER CALLED THIS PLACE HOME."));
    }

    private void GatherPetal()
    {
        if (save.StoryStep < 2) { Notify("NOTHING HERE NEEDS GATHERING YET."); return; }
        if (save.MoonPetals >= 3) { Notify("YOU HAVE ALL THREE MOON PETALS."); return; }
        save.MoonPetals++;
        if (save.MoonPetals == 3) { save.StoryStep = 3; Notify("THREE MOON PETALS! TAKE THEM TO BOB."); }
        else Notify("FOUND A MOON PETAL! " + save.MoonPetals + " / 3");
    }

    private void Farm()
    {
        if (save.Character.Energy < 5) { Notify("TOO TIRED. GET SOME REST."); return; }
        var crop = save.Crops.FirstOrDefault(c => Vector2.Distance(player, new Vector2(c.X, c.Y)) < 35);
        if (crop == null)
        {
            if (save.Character.Seeds <= 0) { Notify("YOU NEED MORE SEEDS."); return; }
            int x = (int)(player.X / 24) * 24, y = (int)(player.Y / 24) * 24;
            save.Crops.Add(new CropData { X = x, Y = y });
            save.Character.Seeds--; save.Character.Energy -= 5; Notify("PLANTED A LITTLE SEED. WATER IT TOMORROW!");
        }
        else if (!crop.WateredToday) { crop.WateredToday = true; save.Character.Energy -= 3; Notify("YOUR CROP HAS BEEN WATERED!"); }
        else if (crop.DaysGrown >= 3) { save.Crops.Remove(crop); save.Character.Money += 40; Notify("HARVESTED A MOONBERRY! +40"); }
        else Notify("A LITTLE MORE TIME AND SUNSHINE.");
    }

    private void OpenComputer()
    {
        OpenPanel("MOONDROP HOME COMPUTER", new[] { "INBOX", "MEADOWNET NEWS", "JOB BOARD", "BUSINESS DESK", "LANDLINK PROPERTY", "BANK SAVINGS", "CLOSE" }, i =>
        {
            if (i == 0) ShowMail();
            if (i == 1) ReadPaper();
            if (i == 2) OpenPanel("LOCAL JOB BOARD", new[] { "TAKE A DELIVERY SHIFT +35", "WORK AT THE CAFE +30", "CHOOSE A CAREER", "CLOSE" }, j =>
            {
                if (j == 0 || j == 1) { save.Character.Money += j == 0 ? 35 : 30; save.Character.Energy = Math.Max(0, save.Character.Energy - 10); Notify("SHIFT COMPLETE. PAY RECEIVED!"); }
                if (j == 2) OpenPanel("CAREER PATHS", new[] { "PROGRAMMER", "CHEF", "BUILDER", "ARTIST", "JOURNALIST", "TEACHER", "FARMER" }, n => { save.Character.Career = new[] { "Programmer", "Chef", "Builder", "Artist", "Journalist", "Teacher", "Farmer" }[n]; Notify("CAREER INTEREST: " + save.Character.Career); });
            });
            if (i == 3) BusinessScene();
            if (i == 4) { if (save.Character.Money >= 250) { save.Character.Money -= 250; save.OwnedProperties.Add("Riverside Lot"); Notify("YOU BOUGHT A RIVERSIDE LOT!"); } else Notify("A SMALL LOT COSTS 250. KEEP SAVING!"); }
            if (i == 5) OpenPanel("MOONDROP COMMUNITY BANK", new[] { "DEPOSIT 100", "WITHDRAW 100", "CLOSE" }, j => { if (j == 0 && save.Character.Money >= 100) { save.Character.Money -= 100; save.Savings += 100; } if (j == 1 && save.Savings >= 100) { save.Savings -= 100; save.Character.Money += 100; } Notify("SAVINGS: " + save.Savings); });
        });
    }

    private void BusinessScene()
    {
        if (save.Business == null)
            OpenPanel("START SOMETHING YOURS", new[] { "OPEN A WORKSHOP - 200", "OPEN A CAFE - 250", "OPEN A SOFTWARE STUDIO - 300", "MAYBE LATER" }, i =>
            {
                int cost = new[] { 200, 250, 300, 0 }[i];
                if (i < 3 && save.Character.Money >= cost)
                {
                    save.Character.Money -= cost;
                    string type = new[] { "Workshop", "Cafe", "Software Studio" }[i];
                    save.Business = new BusinessData { Type = type, Name = "MoonDrop " + type };
                    Notify("YOUR " + type.ToUpperInvariant() + " IS OPEN!");
                }
                else if (i < 3) Notify("YOU NEED MORE SAVINGS TO START.");
            });
        else
            OpenPanel(save.Business.Name.ToUpperInvariant(), new[] { "COLLECT DAILY SALES", "HIRE JUNIPER - 40", "HIRE THEO - 40", "UPGRADE BUSINESS - 150", "CLOSE" }, i =>
            {
                if (i == 0) { save.Character.Money += save.Business.DailyRevenue; Notify("BUSINESS EARNINGS: +" + save.Business.DailyRevenue); }
                if ((i == 1 || i == 2) && save.Character.Money >= 40) { save.Character.Money -= 40; save.Business.Employees.Add(i == 1 ? "Juniper" : "Theo"); Notify("NEW EMPLOYEE HIRED!"); }
                if (i == 3 && save.Character.Money >= 150) { save.Character.Money -= 150; save.Business.Level++; save.Business.DailyRevenue += 30; Notify("BUSINESS LEVEL UP!"); }
            });
    }

    private void ReadPaper()
    {
        string headline = save.StoryStep >= 2 ? "BLUE LIGHTS SEEN OVER MOONWOOD" : "MAYORLARRY ANNOUNCES SPRING MARKET";
        OpenPanel("THE MOONDROP GAZETTE", new[] { headline, "SCHOOL PLAY AUDITIONS NEXT WEEK", "NEW JOBS AT MARKET ROW", "COMMUNITY DANCE THIS FRIDAY", "CLOSE" }, i =>
        {
            if (i == 0) Notify(save.StoryStep >= 2 ? "THE REPORTER WANTS TO KNOW MORE." : "MAYORLARRY SPEAKS VERY, VERY QUICKLY.");
        });
    }

    private void ShowMail()
    {
        string family = save.Character.HasSibling ? "YOUR SIBLING SENT YOU A MEME ABOUT THE MAYOR." : "MOM AND DAD SAY THEY ARE PROUD OF YOU.";
        OpenPanel("INBOX", new[] { family, "BOB: I HAVE A THEORY. IT MAY INVOLVE A KETTLE.", "NELL: THE GAZETTE WOULD LOVE YOUR STORY.", "CLOSE" }, i => { if (i == 0) Notify("YOU SMILE AT THE MESSAGE."); if (i == 1) Notify("BOB HAS SENT 4 FOLLOW-UP LETTERS."); });
    }

    private void NextDay()
    {
        save.TimeMinutes = 7 * 60;
        save.DayOfYear++;
        save.TotalDaysLivedHere++;
        save.Character.Energy = Math.Min(100, save.Character.Energy + 45);
        foreach (var crop in save.Crops) { if (crop.WateredToday) crop.DaysGrown++; crop.WateredToday = false; }
        if (save.DayOfYear > 112)
        {
            save.DayOfYear = 1; save.Year++; save.Character.Age++;
            foreach (var child in save.Children) child.Age++;
            Notify("HAPPY NEW YEAR! HAPPY BIRTHDAY, " + save.Character.Name.ToUpperInvariant() + "!");
        }
        else Notify("A NEW DAY IN MOONDROP MEADOWS");
        save.Mail.Insert(0, "A fresh newspaper is waiting at the Gazette.");
    }

    private void SaveGame()
    {
        try
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoonDropMeadows");
            Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "save.json"), JsonSerializer.Serialize(save, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch { Notify("SAVE FAILED. CHECK FILE PERMISSIONS."); }
    }

    private void LoadSave()
    {
        try
        {
            string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MoonDropMeadows", "save.json");
            if (File.Exists(path))
            {
                save = JsonSerializer.Deserialize<GameSave>(File.ReadAllText(path)) ?? new GameSave();
                nameBuffer = save.Character.Name;
                ageChoice = save.Character.Age;
                siblingChoice = save.Character.HasSibling;
                characterSetup = false;
                Notify("WELCOME BACK, " + save.Character.Name.ToUpperInvariant());
            }
        }
        catch { save = new GameSave(); }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(29, 43, 48));
        batch.Begin(samplerState: SamplerState.PointClamp);
        DrawWorld();
        if (characterSetup) DrawCreation();
        else DrawHud();
        if (panelTitle.Length > 0) DrawPanel();
        if (toastTimer > 0 && panelTitle.Length == 0 && !characterSetup)
        {
            Box(170, 565, 620, 38, new Color(22, 31, 40, 230));
            PixelFont.Draw(batch, pixel, toast, 185, 579, Color.White, 2);
        }
        batch.End();
        base.Draw(gameTime);
    }

    private void DrawWorld()
    {
        Box(0, 0, 960, 640, new Color(103, 157, 103));
        for (int y = 0; y < 640; y += 32)
            for (int x = 0; x < 960; x += 32)
                if (((x / 32) * 7 + (y / 32) * 13) % 5 == 0) Box(x, y, 32, 32, new Color(110, 165, 108));
        Box(0, 195, 960, 42, new Color(198, 174, 119));
        Box(0, 385, 960, 38, new Color(198, 174, 119));
        Box(300, 0, 38, 640, new Color(198, 174, 119));
        Box(600, 0, 38, 640, new Color(198, 174, 119));
        foreach (var p in places) DrawBuilding(p.Key, p.Value);
        foreach (var crop in save.Crops)
        {
            Box(crop.X - 5, crop.Y - 5, 20, 20, new Color(110, 76, 48));
            Box(crop.X, crop.Y, 10, 8 + crop.DaysGrown * 3, crop.DaysGrown >= 3 ? Color.Gold : new Color(86, 190, 91));
        }
        DrawPerson((int)player.X, (int)player.Y, new Color(241, 191, 139), new Color(70, 76, 150));
        if (dancing)
        {
            danceClock -= (float)gameTimeSafeDelta;
            if (danceClock <= 0) dancing = false;
            PixelFont.Draw(batch, pixel, "* DANCE TIME *", (int)player.X - 60, (int)player.Y - 42, Color.HotPink, 2);
        }
    }

    private double gameTimeSafeDelta = 0.016;

    private void DrawBuilding(string name, Rectangle r)
    {
        Color wall = name switch
        {
            "HOME" => new Color(226, 190, 139),
            "SCHOOL" => new Color(218, 203, 142),
            "BOB'S TOWER" => new Color(135, 111, 169),
            "MAYOR'S OFFICE" => new Color(196, 158, 127),
            _ => new Color(213, 183, 142)
        };
        Box(r.X, r.Y + 20, r.Width, r.Height - 20, wall);
        Box(r.X - 6, r.Y + 12, r.Width + 12, 18, new Color(111, 73, 79));
        Box(r.X + r.Width / 2 - 12, r.Y + r.Height - 34, 24, 34, new Color(91, 63, 58));
        Box(r.X + 18, r.Y + 42, 24, 22, new Color(116, 183, 201));
        Box(r.Right - 42, r.Y + 42, 24, 22, new Color(116, 183, 201));
        PixelFont.Draw(batch, pixel, name, r.X - 8, r.Y - 3, Color.White, 1);
    }

    private void DrawPerson(int x, int y, Color skin, Color shirt)
    {
        Box(x - 8, y - 20, 16, 16, skin);
        Box(x - 10, y - 5, 20, 20, shirt);
        Box(x - 9, y + 14, 7, 10, new Color(61, 57, 66));
        Box(x + 2, y + 14, 7, 10, new Color(61, 57, 66));
        Box(x - 8, y - 21, 16, 5, new Color(81, 57, 51));
    }

    private void DrawHud()
    {
        Box(0, 0, 960, 48, new Color(24, 34, 43, 235));
        PixelFont.Draw(batch, pixel, save.Character.Name + "  AGE " + save.Character.Age, 12, 9, Color.White, 2);
        PixelFont.Draw(batch, pixel, currentSeason + " DAY " + save.DayOfYear + " YR " + save.Year, 355, 9, Color.LightGoldenrodYellow, 2);
        PixelFont.Draw(batch, pixel, timeText, 720, 9, Color.White, 2);
        PixelFont.Draw(batch, pixel, "$" + save.Character.Money + "  ENERGY " + save.Character.Energy, 12, 58, Color.White, 2);
        PixelFont.Draw(batch, pixel, "WASD MOVE  E INTERACT  F FARM  C COMPUTER  F5 SAVE", 12, 620, Color.White, 1);
    }

    private void DrawCreation()
    {
        Box(0, 0, 960, 640, new Color(28, 42, 54));
        PixelFont.Draw(batch, pixel, "MOONDROP MEADOWS", 240, 100, new Color(247, 219, 155), 4);
        PixelFont.Draw(batch, pixel, "A LIFE THAT IS YOURS TO WRITE", 290, 145, Color.White, 2);
        PixelFont.Draw(batch, pixel, "CHARACTER NAME: " + nameBuffer, 260, 235, Color.White, 3);
        PixelFont.Draw(batch, pixel, editingName ? "TYPE YOUR NAME THEN PRESS ENTER" : "PRESS N TO EDIT NAME", 260, 270, Color.LightGray, 2);
        PixelFont.Draw(batch, pixel, "STARTING AGE: " + ageChoice + "  (UP / DOWN)", 260, 330, Color.White, 3);
        PixelFont.Draw(batch, pixel, "SIBLING: " + (siblingChoice ? "YES" : "NO") + "  (F TO TOGGLE)", 260, 385, Color.White, 3);
        PixelFont.Draw(batch, pixel, "ENTER THE TOWN", 355, 475, Color.LightGoldenrodYellow, 3);
        PixelFont.Draw(batch, pixel, "SCHOOL, WORK, FRIENDS, FAMILY, AND A LITTLE MAGIC.", 180, 545, Color.White, 2);
    }

    private void DrawPanel()
    {
        Box(130, 115, 700, 415, new Color(24, 32, 43, 245));
        Box(138, 123, 684, 399, new Color(58, 75, 82));
        PixelFont.Draw(batch, pixel, panelTitle, 165, 150, new Color(250, 220, 152), 3);
        for (int i = 0; i < panelLines.Length; i++)
        {
            Color c = i == panelIndex ? Color.Yellow : Color.White;
            PixelFont.Draw(batch, pixel, (i == panelIndex ? "> " : "  ") + panelLines[i], 175, 205 + i * 38, c, 2);
        }
        PixelFont.Draw(batch, pixel, "UP DOWN SELECT  ENTER CHOOSE  ESC CLOSE", 165, 485, Color.LightGray, 1);
    }

    private void Box(int x, int y, int w, int h, Color color) => batch.Draw(pixel, new Rectangle(x, y, w, h), color);
}
