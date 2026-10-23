using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Windows.Data.Json;
using Windows.Devices.Sensors;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace FitnessTracker
{
    public sealed partial class MainPage : Page
    {
        private readonly ApplicationDataContainer settings = ApplicationData.Current.LocalSettings;
        private readonly List<LogEntry> entries = new List<LogEntry>();
        private Pedometer pedometer;
        private int stepGoal = 8000;
        private int calorieGoal = 2000;
        private double weightKg = 70.0;
        private double strideCm = 75.0;

        private sealed class LogEntry
        {
            public string Day;
            public string Time;
            public string Type;
            public string Name;
            public double Calories;
            public double DistanceKm;
            public double DurationMinutes;
            public double Quantity;
        }

        public MainPage()
        {
            InitializeComponent();
            LoadEntries();
            LoadGoals();
            UpdateDashboard();
            Loaded += MainPage_Loaded;
        }

        private async void MainPage_Loaded(object sender, RoutedEventArgs e)
        {
            if (pedometer != null) return;
            try
            {
                pedometer = await Pedometer.GetDefaultAsync();
                if (pedometer == null)
                {
                    SensorStatusText.Text = "No built-in pedometer found. Use Add steps or sync activity manually.";
                    return;
                }

                pedometer.ReadingChanged += Pedometer_ReadingChanged;
                SensorStatusText.Text = "Step sensor connected. The app syncs available readings when opened.";
                try
                {
                    foreach (PedometerReading reading in pedometer.GetCurrentReadings().Values)
                    {
                        string kind = reading.StepKind.ToString();
                        if (kind == "Walking" || kind == "Running")
                            ApplySensorReading(kind, Convert.ToInt64(reading.CumulativeSteps, CultureInfo.InvariantCulture));
                    }
                    UpdateDashboard();
                }
                catch
                {
                    // Keep live reading updates enabled if the device cannot return an initial reading.
                }
            }
            catch
            {
                SensorStatusText.Text = "Step sensor unavailable on this phone. Manual step entry still works.";
            }
        }

        private async void Pedometer_ReadingChanged(Pedometer sender, PedometerReadingChangedEventArgs args)
        {
            string kind = args.Reading.StepKind.ToString();
            long cumulative = Convert.ToInt64(args.Reading.CumulativeSteps, CultureInfo.InvariantCulture);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (kind != "Walking" && kind != "Running") return;
                ApplySensorReading(kind, cumulative);
                UpdateDashboard();
            });
        }

        private void ApplySensorReading(string kind, long cumulative)
        {
            string day = DayKey();
            string sensorDayKey = "sensor_steps_day";
            if (GetString(sensorDayKey, "") != day)
            {
                settings.Values[sensorDayKey] = day;
                settings.Values["sensor_steps"] = 0;
            }

            string lastDayKey = "sensor_last_day_" + kind.ToLowerInvariant();
            string lastCountKey = "sensor_last_count_" + kind.ToLowerInvariant();
            int daily = Math.Max(0, GetInt("sensor_steps", 0));
            if (GetString(lastDayKey, "") == day)
            {
                long previous = GetLong(lastCountKey, cumulative);
                long difference = cumulative - previous;
                if (difference > 0 && difference < 10000)
                    daily = checked(daily + (int)difference);
            }

            settings.Values["sensor_steps"] = daily;
            settings.Values[lastCountKey] = cumulative;
            settings.Values[lastDayKey] = day;
        }

        private void LoadEntries()
        {
            entries.Clear();
            try
            {
                object raw = settings.Values["fitness_log"];
                if (raw == null) return;
                JsonArray array = JsonArray.Parse(raw as string);
                foreach (IJsonValue value in array)
                {
                    if (value.ValueType != JsonValueType.Object) continue;
                    JsonObject item = value.GetObject();
                    entries.Add(new LogEntry
                    {
                        Day = ReadJsonString(item, "day"),
                        Time = ReadJsonString(item, "time"),
                        Type = ReadJsonString(item, "type"),
                        Name = ReadJsonString(item, "name"),
                        Calories = ReadJsonNumber(item, "calories"),
                        DistanceKm = ReadJsonNumber(item, "distance"),
                        DurationMinutes = ReadJsonNumber(item, "duration"),
                        Quantity = ReadJsonNumber(item, "quantity")
                    });
                }
            }
            catch
            {
                entries.Clear();
            }
        }

        private static string ReadJsonString(JsonObject obj, string key)
        {
            try { return obj.GetNamedString(key, ""); }
            catch { return ""; }
        }

        private static double ReadJsonNumber(JsonObject obj, string key)
        {
            try { return obj.GetNamedNumber(key, 0); }
            catch { return 0; }
        }

        private void SaveEntries()
        {
            JsonArray array = new JsonArray();
            foreach (LogEntry entry in entries)
            {
                JsonObject item = new JsonObject();
                item["day"] = JsonValue.CreateStringValue(entry.Day ?? "");
                item["time"] = JsonValue.CreateStringValue(entry.Time ?? "");
                item["type"] = JsonValue.CreateStringValue(entry.Type ?? "");
                item["name"] = JsonValue.CreateStringValue(entry.Name ?? "");
                item["calories"] = JsonValue.CreateNumberValue(entry.Calories);
                item["distance"] = JsonValue.CreateNumberValue(entry.DistanceKm);
                item["duration"] = JsonValue.CreateNumberValue(entry.DurationMinutes);
                item["quantity"] = JsonValue.CreateNumberValue(entry.Quantity);
                array.Add(item);
            }
            settings.Values["fitness_log"] = array.Stringify();
        }

        private void LoadGoals()
        {
            stepGoal = Math.Max(500, GetInt("goal_steps", 8000));
            calorieGoal = Math.Max(500, GetInt("goal_calories", 2000));
            weightKg = GetDouble("weight_kg", 70.0);
            strideCm = GetDouble("stride_cm", 75.0);
            StepGoalBox.Text = stepGoal.ToString(CultureInfo.InvariantCulture);
            CalorieGoalBox.Text = calorieGoal.ToString(CultureInfo.InvariantCulture);
            WeightBox.Text = weightKg.ToString("0.#", CultureInfo.InvariantCulture);
            StrideBox.Text = strideCm.ToString("0.#", CultureInfo.InvariantCulture);
        }

        private void UpdateDashboard()
        {
            string day = DayKey();
            if (GetString("sensor_steps_day", "") != day)
            {
                settings.Values["sensor_steps_day"] = day;
                settings.Values["sensor_steps"] = 0;
            }

            int steps = Math.Max(0, GetInt("sensor_steps", 0)) + GetInt("manual_steps_" + day, 0);
            List<LogEntry> todayFood = TodayEntries("Food");
            List<LogEntry> todayActivity = entries.Where(x => x.Day == day && x.Type == "Activity").ToList();
            double foodCalories = todayFood.Sum(x => x.Calories);
            double loggedExerciseCalories = todayActivity.Sum(x => x.Calories);
            double stepBurn = steps * weightKg * 0.00045;
            // Use the larger estimate to avoid obviously double-counting a logged run
            // that was also included by the phone's pedometer.
            double burned = Math.Max(stepBurn, loggedExerciseCalories);
            double distanceKm = steps * strideCm / 100.0 / 1000.0;

            DateText.Text = DateTime.Now.ToString("dddd, MMMM d", CultureInfo.CurrentCulture);
            StepsText.Text = steps.ToString("N0", CultureInfo.CurrentCulture);
            StepGoalText.Text = "Goal: " + stepGoal.ToString("N0", CultureInfo.CurrentCulture) + " steps";
            StepProgress.Value = Math.Min(100, steps * 100.0 / Math.Max(1, stepGoal));
            DistanceText.Text = distanceKm.ToString("0.00", CultureInfo.CurrentCulture) + " km";
            BurnedText.Text = Math.Round(burned).ToString("N0", CultureInfo.CurrentCulture) + " kcal";
            FoodCaloriesText.Text = Math.Round(foodCalories).ToString("N0", CultureInfo.CurrentCulture) + " kcal";
            FoodPageCaloriesText.Text = FoodCaloriesText.Text;
            FoodGoalText.Text = "Goal: " + calorieGoal.ToString("N0", CultureInfo.CurrentCulture) + " kcal";
            FoodProgress.Value = Math.Min(100, foodCalories * 100.0 / Math.Max(1, calorieGoal));
            BalanceCaloriesText.Text = Math.Round(foodCalories - burned).ToString("N0", CultureInfo.CurrentCulture) + " kcal";
            WaterText.Text = GetInt("water_" + day, 0).ToString(CultureInfo.CurrentCulture) + " / 8 glasses";

            settings.Values["day_steps_" + day] = steps;
            settings.Values["day_distance_" + day] = distanceKm;
            settings.Values["day_food_" + day] = Math.Round(foodCalories);
            settings.Values["day_burn_" + day] = Math.Round(burned);

            ActivityList.ItemsSource = todayActivity
                .OrderByDescending(x => x.Time)
                .Select(ActivityDescription).ToList();
            FoodList.ItemsSource = todayFood
                .OrderByDescending(x => x.Time)
                .Select(FoodDescription).ToList();
            HistoryList.ItemsSource = BuildHistory();
        }

        private List<string> BuildHistory()
        {
            List<string> result = new List<string>();
            DateTime today = DateTime.Now.Date;
            for (int i = 0; i < 14; i++)
            {
                string day = today.AddDays(-i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                object savedSteps = settings.Values["day_steps_" + day];
                if (savedSteps == null) continue;
                result.Add(today.AddDays(-i).ToString("ddd, MMM d", CultureInfo.CurrentCulture)
                    + " — " + Convert.ToInt32(savedSteps, CultureInfo.InvariantCulture).ToString("N0", CultureInfo.CurrentCulture)
                    + " steps · " + GetDouble("day_distance_" + day, 0).ToString("0.00", CultureInfo.CurrentCulture)
                    + " km · " + Math.Round(GetDouble("day_food_" + day, 0)).ToString(CultureInfo.CurrentCulture) + " kcal eaten");
            }

            result.AddRange(entries.OrderByDescending(x => x.Day).ThenByDescending(x => x.Time)
                .Take(80).Select(x => x.Day + " " + x.Time + " · " + EntryDescription(x)));
            if (result.Count == 0) result.Add("Your activity and meal history will appear here.");
            return result;
        }

        private List<LogEntry> TodayEntries(string type)
        {
            return entries.Where(x => x.Day == DayKey() && x.Type == type).ToList();
        }

        private static string ActivityDescription(LogEntry entry)
        {
            return entry.Time + " · " + entry.Name + " — " + entry.DistanceKm.ToString("0.##", CultureInfo.CurrentCulture)
                + " km, " + entry.DurationMinutes.ToString("0", CultureInfo.CurrentCulture) + " min · ~"
                + Math.Round(entry.Calories).ToString("N0", CultureInfo.CurrentCulture) + " kcal";
        }

        private static string FoodDescription(LogEntry entry)
        {
            return entry.Time + " · " + entry.Name + " — " + Math.Round(entry.Calories).ToString("N0", CultureInfo.CurrentCulture) + " kcal";
        }

        private static string EntryDescription(LogEntry entry)
        {
            if (entry.Type == "Food") return "Food: " + entry.Name + " — " + Math.Round(entry.Calories).ToString("N0", CultureInfo.CurrentCulture) + " kcal";
            return entry.Name + " — " + entry.DistanceKm.ToString("0.##", CultureInfo.CurrentCulture) + " km · "
                + Math.Round(entry.Calories).ToString("N0", CultureInfo.CurrentCulture) + " kcal";
        }

        private void AddSteps_Click(object sender, RoutedEventArgs e)
        {
            int amount;
            if (!int.TryParse(ManualStepsBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out amount)
                || amount <= 0 || amount > 100000)
            {
                SensorStatusText.Text = "Enter a step amount from 1 to 100,000.";
                return;
            }

            string key = "manual_steps_" + DayKey();
            settings.Values[key] = checked(GetInt(key, 0) + amount);
            ManualStepsBox.Text = "";
            SensorStatusText.Text = amount.ToString("N0", CultureInfo.CurrentCulture) + " steps added.";
            UpdateDashboard();
        }

        private void AddWater_Click(object sender, RoutedEventArgs e)
        {
            string key = "water_" + DayKey();
            settings.Values[key] = Math.Min(99, GetInt(key, 0) + 1);
            UpdateDashboard();
        }

        private void SaveActivity_Click(object sender, RoutedEventArgs e)
        {
            double distance;
            double minutes;
            if (!TryReadNumber(ActivityDistanceBox.Text, out distance) || distance <= 0 || distance > 500
                || !TryReadNumber(ActivityDurationBox.Text, out minutes) || minutes <= 0 || minutes > 1440)
            {
                ActivityStatusText.Text = "Enter a distance over 0 km and a duration between 1 and 1,440 minutes.";
                return;
            }

            ComboBoxItem selected = ActivityTypeBox.SelectedItem as ComboBoxItem;
            string activity = selected == null ? "Run" : Convert.ToString(selected.Content, CultureInfo.CurrentCulture);
            double factor = activity == "Run" ? 1.0 : (activity == "Walk" ? 0.65 : 0.5);
            double burned = Math.Round(distance * weightKg * factor);
            entries.Add(new LogEntry
            {
                Day = DayKey(),
                Time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
                Type = "Activity",
                Name = activity,
                Calories = burned,
                DistanceKm = distance,
                DurationMinutes = minutes,
                Quantity = 1
            });
            SaveEntries();
            ActivityDistanceBox.Text = "";
            ActivityDurationBox.Text = "";
            ActivityStatusText.Text = activity + " saved · estimated " + burned.ToString("N0", CultureInfo.CurrentCulture) + " kcal.";
            UpdateDashboard();
        }

        private void AddFood_Click(object sender, RoutedEventArgs e)
        {
            string name = (FoodNameBox.Text ?? "").Trim();
            double calories;
            if (name.Length == 0 || name.Length > 100
                || !TryReadNumber(FoodCaloriesBox.Text, out calories) || calories <= 0 || calories > 20000)
            {
                FoodStatusText.Text = "Enter a food name and calories between 1 and 20,000.";
                return;
            }

            ComboBoxItem selected = MealTypeBox.SelectedItem as ComboBoxItem;
            string meal = selected == null ? "Food" : Convert.ToString(selected.Content, CultureInfo.CurrentCulture);
            entries.Add(new LogEntry
            {
                Day = DayKey(),
                Time = DateTime.Now.ToString("HH:mm", CultureInfo.InvariantCulture),
                Type = "Food",
                Name = meal + ": " + name,
                Calories = Math.Round(calories),
                Quantity = 1
            });
            SaveEntries();
            FoodNameBox.Text = "";
            FoodCaloriesBox.Text = "";
            FoodStatusText.Text = "Food added to today's log.";
            UpdateDashboard();
        }

        private void SaveGoals_Click(object sender, RoutedEventArgs e)
        {
            int newStepGoal;
            int newCalorieGoal;
            double newWeight;
            double newStride;
            if (!int.TryParse(StepGoalBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out newStepGoal)
                || newStepGoal < 500 || newStepGoal > 100000
                || !int.TryParse(CalorieGoalBox.Text, NumberStyles.Integer, CultureInfo.CurrentCulture, out newCalorieGoal)
                || newCalorieGoal < 500 || newCalorieGoal > 10000
                || !TryReadNumber(WeightBox.Text, out newWeight) || newWeight < 20 || newWeight > 400
                || !TryReadNumber(StrideBox.Text, out newStride) || newStride < 20 || newStride > 150)
            {
                GoalsStatusText.Text = "Check your values: 500–100,000 steps, 500–10,000 food kcal, 20–400 kg, and 20–150 cm stride.";
                return;
            }

            stepGoal = newStepGoal;
            calorieGoal = newCalorieGoal;
            weightKg = newWeight;
            strideCm = newStride;
            settings.Values["goal_steps"] = stepGoal;
            settings.Values["goal_calories"] = calorieGoal;
            settings.Values["weight_kg"] = weightKg;
            settings.Values["stride_cm"] = strideCm;
            GoalsStatusText.Text = "Goals and body weight saved on this phone.";
            UpdateDashboard();
        }

        private async void Export_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                FileSavePicker picker = new FileSavePicker();
                picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
                picker.SuggestedFileName = "FitTrack-" + DayKey();
                picker.FileTypeChoices.Add("CSV spreadsheet", new List<string> { ".csv" });
                StorageFile file = await picker.PickSaveFileAsync();
                if (file == null) return;

                StringBuilder csv = new StringBuilder();
                csv.AppendLine("date,time,type,name,calories,distance_km,duration_minutes,quantity");
                foreach (LogEntry item in entries.OrderBy(x => x.Day).ThenBy(x => x.Time))
                {
                    csv.Append(Csv(item.Day)).Append(',')
                       .Append(Csv(item.Time)).Append(',')
                       .Append(Csv(item.Type)).Append(',')
                       .Append(Csv(item.Name)).Append(',')
                       .Append(item.Calories.ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(item.DistanceKm.ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(item.DurationMinutes.ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(item.Quantity.ToString(CultureInfo.InvariantCulture)).AppendLine();
                }

                csv.AppendLine();
                csv.AppendLine("date,daily_steps,step_distance_km,food_calories,estimated_burn_calories");
                DateTime end = DateTime.Now.Date;
                for (int i = 29; i >= 0; i--)
                {
                    string day = end.AddDays(-i).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                    object saved = settings.Values["day_steps_" + day];
                    if (saved == null) continue;
                    csv.Append(day).Append(',')
                       .Append(Convert.ToInt32(saved, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(GetDouble("day_distance_" + day, 0).ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(GetDouble("day_food_" + day, 0).ToString(CultureInfo.InvariantCulture)).Append(',')
                       .Append(GetDouble("day_burn_" + day, 0).ToString(CultureInfo.InvariantCulture)).AppendLine();
                }

                await FileIO.WriteTextAsync(file, csv.ToString());
                SensorStatusText.Text = "CSV backup saved.";
            }
            catch (Exception ex)
            {
                SensorStatusText.Text = "Export failed: " + ex.Message;
            }
        }

        private static string Csv(string value)
        {
            string text = value ?? "";
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }

        private static bool TryReadNumber(string text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value)) return true;
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static string DayKey()
        {
            return DateTime.Now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        private int GetInt(string key, int fallback)
        {
            try { object value = settings.Values[key]; return value == null ? fallback : Convert.ToInt32(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private long GetLong(string key, long fallback)
        {
            try { object value = settings.Values[key]; return value == null ? fallback : Convert.ToInt64(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private double GetDouble(string key, double fallback)
        {
            try { object value = settings.Values[key]; return value == null ? fallback : Convert.ToDouble(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }

        private string GetString(string key, string fallback)
        {
            try { object value = settings.Values[key]; return value == null ? fallback : Convert.ToString(value, CultureInfo.InvariantCulture); }
            catch { return fallback; }
        }
    }
}
