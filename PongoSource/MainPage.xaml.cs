using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Threading;
using Windows.Networking.BackgroundTransfer;
using System.Threading.Tasks;
using Windows.Media.SpeechRecognition;
using Windows.Media.SpeechSynthesis;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.System;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;

namespace PongoLumia {
  public sealed partial class MainPage : Page {
    private bool busy;
    private bool codeReady;
    private bool localModelLoaded;
    private bool modelBusy;
    private const string ModelFileName = "SmolLM2-135M-Instruct-Q3_K_M.gguf";
    private const string ModelUrl = "https://huggingface.co/tensorblock/SmolLM2-135M-Instruct-GGUF/resolve/main/SmolLM2-135M-Instruct-Q3_K_M.gguf?download=true";

    [DllImport("PongoLlmNative.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int PongoLlmLoad([MarshalAs(UnmanagedType.LPWStr)] string modelPath);

    [DllImport("PongoLlmNative.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern int PongoLlmIsLoaded();

    [DllImport("PongoLlmNative.dll", CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int PongoLlmGenerate(
      [MarshalAs(UnmanagedType.LPWStr)] string prompt,
      [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder output,
      int outputCapacity,
      int maxTokens);

    [DllImport("PongoLlmNative.dll", CallingConvention = CallingConvention.Cdecl, ExactSpelling = true)]
    private static extern void PongoLlmUnload();
    private string currentLanguage = "HTML / JavaScript";
    private readonly ApplicationDataContainer data = ApplicationData.Current.LocalSettings;

    private sealed class KnowledgeItem {
      public string Keywords;
      public string Answer;
      public KnowledgeItem(string keywords, string answer) { Keywords = keywords; Answer = answer; }
    }

    private static readonly KnowledgeItem[] LocalKnowledge = new KnowledgeItem[] {
      new KnowledgeItem("python code language print function loop", "Python is a beginner-friendly programming language. Example:\r\n\r\nname = \"Pongo\"\r\nprint(\"Hello, \" + name)\r\n\r\nUse variables to hold values, if statements to choose, loops to repeat, and functions to reuse code. Try the Python example in Code Lab."),
      new KnowledgeItem("html website webpage markup", "HTML describes the structure of a webpage. A tiny offline page:\r\n\r\n<!doctype html>\r\n<html><body><h1>Hello from Pongo</h1><p>This page works offline.</p></body></html>\r\n\r\nOpen Code Lab, choose HTML / JavaScript, then Run / Preview."),
      new KnowledgeItem("javascript web script function browser", "JavaScript adds logic and interaction to web pages. Example:\r\n\r\nconst button = document.querySelector('button');\r\nbutton.addEventListener('click', () => alert('Pongo says hi!'));\r\n\r\nTry it in an HTML file in Code Lab; the preview runs locally in the app."),
      new KnowledgeItem("csharp programming windows app", "C# is a compiled language commonly used for Windows apps. Example:\r\n\r\nusing System;\r\nclass Program {\r\n  static void Main() { Console.WriteLine(\"Hello, Pongo!\"); }\r\n}\r\n\r\nCode Lab can edit and save C# code. Building an actual .appx requires the Windows SDK and build tools on a PC."),
      new KnowledgeItem("xaml interface layout controls uwp", "XAML describes a Windows app's UI. Example:\r\n\r\n<StackPanel Padding=\"20\">\r\n  <TextBlock Text=\"Hello, Pongo!\" FontSize=\"24\" />\r\n  <Button Content=\"Click me\" />\r\n</StackPanel>\r\n\r\nXAML is normally paired with C# event handlers in a UWP project."),
      new KnowledgeItem("appx package install package windows mobile", "An .appx is a Windows app package. A real UWP .appx contains compiled app files, a manifest, assets, and a signature. Pongo can generate starter project source while offline, but compiling and signing a real ARM .appx needs Windows SDK/MSBuild tooling on a PC."),
      new KnowledgeItem("ai local offline chatbot model", "Without the optional model installed, Pongo uses an offline word-matching helper, notes, maths, and saved lessons. You can download the small 135M-parameter GGUF model once, then load it for on-device language generation without an AI API."),
      new KnowledgeItem("transformer neural network machine learning", "A transformer is a neural-network architecture that uses attention to process sequences. Pongo can run a quantized 135M-parameter transformer locally after its GGUF model is downloaded. It is much smaller and less capable than large cloud models, but replies are generated on-device."),
      new KnowledgeItem("minecraft redstone logic circuit computer", "Minecraft redstone is a game system for building digital logic and simple computers. Redstone torches, repeaters, comparators, and dust can form NOT, AND, OR, and memory circuits. A half adder adds two bits: SUM is XOR and CARRY is AND."),
      new KnowledgeItem("logic gates and or not xor digital", "Logic gates operate on true/false values or binary bits. AND is 1 only when both inputs are 1; OR is 1 when either input is 1; NOT flips a bit; XOR is 1 when the inputs differ. These gates are the basis of adders and processors."),
      new KnowledgeItem("electricity voltage current resistance resistor", "Voltage is electrical potential difference, current is the flow of charge, and resistance limits current. Ohm's law is V = I × R. Use a suitable resistor with an LED, and do not assume a circuit's voltage is safe just because the parts are small."),
      new KnowledgeItem("half adder binary add sum carry", "A half adder adds two binary bits A and B. SUM = A XOR B, and CARRY = A AND B. Its truth table is: 0+0 gives sum 0 carry 0; 0+1 and 1+0 give sum 1 carry 0; 1+1 gives sum 0 carry 1."),
      new KnowledgeItem("variable loop function if statement programming", "In programming, a variable stores a value, an if statement chooses between paths, a loop repeats steps, and a function packages reusable behavior. Example in Python:\r\n\r\nfor i in range(3):\r\n    print(i)\r\n\r\nThis prints 0, 1, and 2."),
      new KnowledgeItem("json data object array programming", "JSON is a text format for structured data. Objects use key/value pairs and arrays hold ordered items. Example: {\"name\":\"Pongo\",\"offline\":true}. Keep JSON valid by using double quotes around property names and text values."),
      new KnowledgeItem("internet website search current news", "Pongo's chat brain itself is offline. For live facts, use the separate Web search button; it opens a browser search and therefore needs an internet connection. The local chatbot does not call an AI API.")
    };

    public MainPage() {
      InitializeComponent();
      Conversation.Text = data.Values.ContainsKey("ChatHistory")
        ? data.Values["ChatHistory"].ToString()
        : "PONGO • ON-DEVICE LANGUAGE MODEL\r\nDownload the optional ~94 MB model once using the button above; it then generates replies on this phone without an AI API. The mini helper works offline even before that.\r\nTry: hello, 7 * 8, teach question => answer, note ..., show notes, or ask about Python, XAML, .appx, AI, electricity, or redstone.\r\n\r\n";
      Status.Text = "Mini offline helper ready • optional local LLM";
      codeReady = false;
      LanguageBox.SelectedIndex = 0;
      currentLanguage = GetSelectedLanguage();
      CodeEditor.Text = TemplateFor(currentLanguage);
      CodeOutput.Text = "Offline preview and code output appear here.\r\n\r\nHTML / JavaScript can run locally. Python, C# and XAML are editable source snippets; choose Save Code or create a starter app project to build on a PC.";
      CodeStatus.Text = "Code and chat work offline. The full model is optional and runs locally after download.";
      codeReady = true;
      RefreshModelButtonAsync();
    }

    private void ShowChat_Click(object sender, RoutedEventArgs e) {
      ChatPanel.Visibility = Visibility.Visible; CodePanel.Visibility = Visibility.Collapsed; ChatComposer.Visibility = Visibility.Visible;
      Status.Text = "Offline brain ready. No API is being used.";
    }

    private void ShowCode_Click(object sender, RoutedEventArgs e) {
      ChatPanel.Visibility = Visibility.Collapsed; CodePanel.Visibility = Visibility.Visible; ChatComposer.Visibility = Visibility.Collapsed;
      CodeStatus.Text = "Editing " + GetSelectedLanguage() + " locally.";
    }

    private async void Send_Click(object sender, RoutedEventArgs e) { await Submit(Prompt.Text); }
    private async void Prompt_KeyDown(object sender, KeyRoutedEventArgs e) {
      if (e.Key == VirtualKey.Enter) { e.Handled = true; await Submit(Prompt.Text); }
    }

    private async void Listen_Click(object sender, RoutedEventArgs e) {
      try {
        Status.Text = "Listening…";
        using (var recognizer = new SpeechRecognizer()) {
          recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(SpeechRecognitionScenario.Dictation, "assistant request"));
          var compilation = await recognizer.CompileConstraintsAsync();
          if (compilation.Status != SpeechRecognitionResultStatus.Success) { Status.Text = "Voice recognition is not ready. Try typing instead."; return; }
          var result = await recognizer.RecognizeWithUIAsync();
          if (result.Status == SpeechRecognitionResultStatus.Success) { Prompt.Text = result.Text; await Submit(result.Text); }
          else Status.Text = "Didn't catch that. Try again or type.";
        }
      } catch { Status.Text = "Voice input isn't available right now."; }
    }

    private async Task Submit(string raw) {
      string question = (raw ?? "").Trim();
      if (question.Length == 0 || busy) return;
      Prompt.Text = "";
      Conversation.Text += "YOU\r\n" + question + "\r\n\r\n";
      busy = true; Status.Text = "Pongo is thinking locally…";
      try {
        bool useLocalModel = localModelLoaded && !IsLocalUtilityCommand(question);
        string answer;
        if (useLocalModel) {
          Status.Text = "Generating with local language model…";
          answer = await Task.Run(() => GenerateLocalLlmReply(question));
        } else {
          answer = await LocalBrainReplyAsync(question);
        }
        Conversation.Text += (useLocalModel ? "PONGO • LOCAL LLM\r\n" : "PONGO • OFFLINE HELPER\r\n") + answer + "\r\n\r\n";
        SaveChat();
        Status.Text = useLocalModel ? "Ready • model inference on phone • no AI API" :
          (localModelLoaded ? "Ready • local model installed; utility handled offline" : "Ready • mini offline helper • no API");
        try {
          string spoken = answer.Length > 240 ? answer.Substring(0, 240) : answer;
          using (var synthesizer = new SpeechSynthesizer()) {
            var stream = await synthesizer.SynthesizeTextToStreamAsync(spoken);
            Speech.SetSource(stream, stream.ContentType); Speech.Play();
          }
        } catch { }
      } catch (Exception ex) {
        Status.Text = "Local reply failed: " + ex.Message;
        Conversation.Text += "PONGO\r\nI couldn't complete that response. If the local model is loaded, try reloading it or a shorter question.\r\n\r\n";
        SaveChat();
      } finally { busy = false; }
    }

    private bool IsLocalUtilityCommand(string question) {
      string l = (question ?? "").ToLowerInvariant().Trim();
      if (l.StartsWith("teach ") || l.StartsWith("note ") || l.StartsWith("remember this ") ||
          l.StartsWith("search ") || l.StartsWith("google ") || l.StartsWith("look up ") || l.StartsWith("open ")) return true;
      if (l == "time" || l.Contains("what time") || l == "current time" ||
          l == "date" || l.Contains("what date") || l.Contains("what day is it") || l == "today" ||
          l.Contains("show my notes") || l == "notes" || l == "my notes" || l == "show notes") return true;
      return Regex.IsMatch(l, @"(-?[0-9]+(?:\.[0-9]+)?)\s*([+\-*/x])\s*(-?[0-9]+(?:\.[0-9]+)?)");
    }

    private string GenerateLocalLlmReply(string question) {
      string system = "You are Pongo, a witty, slightly self-important penguin assistant. Be helpful, clear, safe, and honest. Help explain concepts and write code. Never claim you ran code or accessed the internet. Keep answers concise but useful. If uncertain, say so.";
      string prompt = "<|im_start|>system\n" + system + "<|im_end|>\n" +
        "<|im_start|>user\n" + question + "<|im_end|>\n<|im_start|>assistant\n";
      var output = new StringBuilder(8192);
      int count = PongoLlmGenerate(prompt, output, output.Capacity, 112);
      if (count < 0) throw new InvalidOperationException("Model inference returned error " + count + ".");
      string answer = output.ToString().Trim();
      int stop = answer.IndexOf("<|im_end|>", StringComparison.Ordinal);
      if (stop >= 0) answer = answer.Substring(0, stop).Trim();
      stop = answer.IndexOf("<|endoftext|>", StringComparison.Ordinal);
      if (stop >= 0) answer = answer.Substring(0, stop).Trim();
      if (String.IsNullOrWhiteSpace(answer)) throw new InvalidOperationException("The model produced an empty reply.");
      return answer;
    }

    private async void LocalModel_Click(object sender, RoutedEventArgs e) {
      if (modelBusy || localModelLoaded) return;
      modelBusy = true;
      LocalModelButton.IsEnabled = false;
      try {
        StorageFile modelFile = null;
        try { modelFile = await ApplicationData.Current.LocalFolder.GetFileAsync(ModelFileName); }
        catch {
          Status.Text = "Downloading the 94 MB model. Keep Pongo open and connected to Wi-Fi…";
          StorageFile temp = await ApplicationData.Current.LocalFolder.CreateFileAsync("PongoModel.download", CreationCollisionOption.ReplaceExisting);
          var downloader = new BackgroundDownloader();
          var operation = downloader.CreateDownload(new Uri(ModelUrl), temp);
          var progress = new Progress<DownloadOperation>(op => {
            ulong total = op.Progress.TotalBytesToReceive;
            string message = total > 0
              ? "Downloading local AI model: " + Math.Min(100, (int)(100.0 * op.Progress.BytesReceived / total)).ToString() + "%"
              : "Downloading local AI model…";
            var ignored = Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () => { Status.Text = message; });
          });
          await operation.StartAsync().AsTask(CancellationToken.None, progress);
          await temp.RenameAsync(ModelFileName, NameCollisionOption.ReplaceExisting);
          modelFile = await ApplicationData.Current.LocalFolder.GetFileAsync(ModelFileName);
        }
        Status.Text = "Loading local model. This can take a while on the Lumia…";
        int result = await Task.Run(() => PongoLlmLoad(modelFile.Path));
        if (result != 1 || PongoLlmIsLoaded() != 1)
          throw new InvalidOperationException("The ARM inference engine couldn't load the GGUF model (code " + result + ").");
        localModelLoaded = true;
        LocalModelButton.Content = "Local AI loaded ✓";
        Status.Text = "Real local LLM ready • on-device inference • no API";
        Conversation.Text += "PONGO • SYSTEM\r\nThe 135M-parameter language model is loaded. New questions can now be generated locally on this phone.\r\n\r\n";
        SaveChat();
      } catch (DllNotFoundException) {
        Status.Text = "This app package is missing the native ARM32 inference engine. Install the updated build.";
        LocalModelButton.Content = "Retry local AI setup";
      } catch (Exception ex) {
        Status.Text = "Local model setup failed: " + ex.Message;
        LocalModelButton.Content = "Retry local AI setup";
      } finally {
        modelBusy = false;
        LocalModelButton.IsEnabled = true;
      }
    }

    private async Task RefreshModelButtonAsync() {
      try {
        await ApplicationData.Current.LocalFolder.GetFileAsync(ModelFileName);
        LocalModelButton.Content = "Load downloaded local AI model";
        Status.Text = "Model file is already stored on this phone. Tap to load; inference stays offline.";
      } catch {
        LocalModelButton.Content = "Get local AI model (~94 MB)";
      }
    }

    private async Task<string> LocalBrainReplyAsync(string question) {
      string l = question.ToLowerInvariant().Trim();
      if (l.StartsWith("teach ")) {
        int delimiter = question.IndexOf("=>", StringComparison.Ordinal);
        if (delimiter < 0) return "To teach me, use: teach your question => the answer to remember";
        string lessonQuestion = question.Substring(6, delimiter - 6).Trim();
        string lessonAnswer = question.Substring(delimiter + 2).Trim();
        if (lessonQuestion.Length < 2 || lessonAnswer.Length < 2) return "Please give me both a question and a useful answer. Example: teach what is redstone => Redstone is Minecraft's electrical logic system.";
        await SaveLessonAsync(lessonQuestion, lessonAnswer);
        return "Lesson saved on this phone. I can recall it without Wi-Fi. Look at that: you have successfully educated a penguin.";
      }

      string learned = await FindLessonAnswerAsync(question);
      if (!String.IsNullOrEmpty(learned)) return learned + "\r\n\r\n(From a lesson saved locally on this phone.)";
      if (l == "time" || l.Contains("what time") || l == "current time")
        return "It is " + DateTime.Now.ToString("h:mm tt") + ". You're welcome. I do enjoy being right on time.";
      if (l == "date" || l.Contains("what date") || l.Contains("what day is it") || l == "today")
        return "It is " + DateTime.Now.ToString("dddd, MMMM d, yyyy") + ". You may applaud quietly.";
      if (l.StartsWith("note ") || l.StartsWith("remember this ")) {
        string note = l.StartsWith("note ") ? question.Substring(5) : question.Substring(14);
        string old = data.Values.ContainsKey("Notes") ? data.Values["Notes"].ToString() : "";
        data.Values["Notes"] = (old.Length == 0 ? "" : old + "\r\n") + DateTime.Now.ToString("g") + " | " + note;
        return "Saved your note locally. No server involved. Your attention span may now relax.";
      }
      if (l.Contains("show my notes") || l == "notes" || l == "my notes" || l == "show notes")
        return data.Values.ContainsKey("Notes") && !String.IsNullOrWhiteSpace(data.Values["Notes"].ToString()) ? data.Values["Notes"].ToString() : "No notes yet. Try: note remember to finish my project";
      if (l.StartsWith("search ") || l.StartsWith("google ") || l.StartsWith("look up ")) {
        string query = l.StartsWith("search ") ? question.Substring(7) : (l.StartsWith("google ") ? question.Substring(7) : question.Substring(8));
        await Launcher.LaunchUriAsync(new Uri("https://www.bing.com/search?q=" + Uri.EscapeDataString(query)));
        return "I opened a normal browser search for \"" + query + "\". That's optional and online; my offline brain didn't use an API.";
      }
      if (l.StartsWith("open ")) {
        string site = l.Substring(5).Trim();
        string url = site.Contains("youtube") ? "https://m.youtube.com/" :
          site.Contains("wikipedia") ? "https://en.wikipedia.org/" :
          site.Contains("github") ? "https://github.com/" :
          site.Contains("google") ? "https://www.google.com/" :
          site.Contains("hugging face") || site.Contains("huggingface") ? "https://huggingface.co/" : null;
        if (url != null) { await Launcher.LaunchUriAsync(new Uri(url)); return "Opening " + site + ". Naturally, I selected a quality destination."; }
        return "Try: open YouTube, Wikipedia, GitHub, or Google. This shortcut needs internet.";
      }
      if (l == "hi" || l == "hello" || l == "hey" || l == "yo" || l.Contains("good morning"))
        return "Hello. Pongo here: penguin, offline assistant, and clearly the most qualified creature in this room. What's up?";
      if (l.Contains("who are you") || l.Contains("your name") || l == "what are you")
        return "I'm Pongo, the slightly self-important penguin. Without the extra model I use a tiny offline helper. After you download the 135M-parameter local model, I can generate language on this phone too. No AI API or account is needed.";
      if (l == "help" || l.Contains("what can you do") || l.Contains("how do i use you"))
        return "Tap 'Get local AI model' to download a 135M-parameter transformer (~94 MB) for on-device generation. After setup it works offline without an API. You can also use '7 * 8', 'note ...', 'show my notes', or 'teach what is redstone => ...'. Code Lab edits HTML/JavaScript, Python, C#, and XAML.";
      if (l.Contains("how are you")) return "Excellent. My feathers are pristine and my tiny brain is entirely on-device. How are you?";
      if (l.Contains("thank")) return "You're welcome. Compliments are also welcome, though I prefer accurate ones.";

      var math = Regex.Match(l, @"(-?[0-9]+(?:\.[0-9]+)?)\s*([+\-*/x])\s*(-?[0-9]+(?:\.[0-9]+)?)");
      if (math.Success) {
        double x, y;
        if (Double.TryParse(math.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out x) &&
            Double.TryParse(math.Groups[3].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out y)) {
          string op = math.Groups[2].Value;
          double z = op == "+" ? x + y : (op == "-" ? x - y : ((op == "x" || op == "*") ? x * y : (y == 0 ? Double.NaN : x / y)));
          return Double.IsNaN(z) ? "I refuse to divide by zero. Even I have standards." : x.ToString(System.Globalization.CultureInfo.InvariantCulture) + " " + op + " " + y.ToString(System.Globalization.CultureInfo.InvariantCulture) + " = " + z.ToString(System.Globalization.CultureInfo.InvariantCulture) + ". Math complete.";
        }
      }

      if ((l.Contains("write") || l.Contains("make") || l.Contains("example") || l.Contains("code")) && (l.Contains("python") || l.Contains("html") || l.Contains("javascript") || l.Contains("c sharp") || l.Contains("c#"))) {
        if (l.Contains("python")) return "Here's a Python starter you can edit in Code Lab:\r\n\r\nname = \"Pongo\"\r\nfor i in range(3):\r\n    print(\"Hello\", name, i)\r\n\r\nI can't execute Python on this phone, but you can save the source offline.";
        if (l.Contains("html")) return "Here's an offline HTML starter. Open Code Lab and choose Example:\r\n\r\n<!doctype html>\r\n<html><body><h1>Hello from Pongo</h1><button onclick=\"document.getElementById('out').textContent='It works offline!'\">Tap me</button><p id=\"out\"></p></body></html>";
        if (l.Contains("javascript")) return "JavaScript starter:\r\n\r\nconst numbers = [1, 2, 3];\r\nconst doubled = numbers.map(n => n * 2);\r\nconsole.log(doubled);\r\n\r\nFor a visible result, use JavaScript inside an HTML page in Code Lab.";
        return "C# starter:\r\n\r\nusing System;\r\nclass Program { static void Main() { Console.WriteLine(\"Hello, Pongo!\"); } }\r\n\r\nYou can edit and save this in Code Lab; building a Windows app requires the Windows SDK on a PC.";
      }

      if (l.Contains("code lab") || l.Contains(".appx") || l.Contains("app package"))
        return "Code Lab works offline. It edits HTML/JavaScript, Python, C#, and XAML. HTML/JS can run in the embedded preview. 'Create a starter .appx project' exports source files and assets to a chosen folder. This Lumia app cannot compile/sign a real .appx by itself; that needs Windows SDK/MSBuild on a PC.";
      string knowledge = LocalKnowledgeReply(l);
      if (!String.IsNullOrEmpty(knowledge)) return knowledge;
      return "I couldn't find a strong match in the mini offline helper. Install the local language model using the button above for generated answers and code. Or teach me with: teach your question => its answer. Web search is a separate optional browser action; the local model itself uses no AI API.";
    }

    private string LocalKnowledgeReply(string question) {
      HashSet<string> queryWords = MeaningfulWords(question);
      if (queryWords.Count == 0) return "";
      double bestScore = 0;
      string bestAnswer = "";
      foreach (var item in LocalKnowledge) {
        HashSet<string> keyWords = MeaningfulWords(item.Keywords);
        int shared = 0;
        foreach (string word in queryWords) if (keyWords.Contains(word)) shared++;
        double score = shared / Math.Sqrt(queryWords.Count * Math.Max(1, keyWords.Count));
        if (score > bestScore) { bestScore = score; bestAnswer = item.Answer; }
      }
      return bestScore >= 0.22 ? bestAnswer : "";
    }

    private HashSet<string> MeaningfulWords(string text) {
      var words = new HashSet<string>();
      var stop = new HashSet<string>(new string[] { "the", "and", "for", "are", "was", "were", "what", "why", "how", "can", "does", "did", "you", "your", "with", "from", "that", "this", "have", "has", "had", "about", "into", "make", "tell", "me", "please", "is", "it", "a", "an", "i", "of", "to", "in", "on", "be", "do", "explain", "show", "give", "help" });
      foreach (Match match in Regex.Matches((text ?? "").ToLowerInvariant(), @"[a-z0-9_+#.-]{2,}")) {
        string word = match.Value.Trim('.', '-', '+');
        if (word.Length > 1 && !stop.Contains(word)) words.Add(word);
      }
      return words;
    }

    private async Task SaveLessonAsync(string question, string answer) {
      StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync("PongoLessons.txt", CreationCollisionOption.OpenIfExists);
      string line = Convert.ToBase64String(Encoding.UTF8.GetBytes(question)) + "\t" + Convert.ToBase64String(Encoding.UTF8.GetBytes(answer)) + "\r\n";
      await FileIO.AppendTextAsync(file, line);
    }

    private async Task<string> FindLessonAnswerAsync(string question) {
      try {
        StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync("PongoLessons.txt");
        string[] lines = (await FileIO.ReadTextAsync(file)).Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        HashSet<string> requested = MeaningfulWords(question);
        string bestAnswer = "";
        double bestScore = 0;
        foreach (string line in lines) {
          string[] parts = line.Split('\t');
          if (parts.Length != 2) continue;
          string savedQuestion = Encoding.UTF8.GetString(Convert.FromBase64String(parts[0]));
          string savedAnswer = Encoding.UTF8.GetString(Convert.FromBase64String(parts[1]));
          if (NormalizeQuestion(savedQuestion) == NormalizeQuestion(question)) return savedAnswer;
          HashSet<string> lessonWords = MeaningfulWords(savedQuestion);
          if (requested.Count == 0 || lessonWords.Count == 0) continue;
          int shared = 0;
          foreach (string word in requested) if (lessonWords.Contains(word)) shared++;
          double score = shared / (double)requested.Count;
          if (score > bestScore) { bestScore = score; bestAnswer = savedAnswer; }
        }
        return bestScore >= 0.78 ? bestAnswer : "";
      } catch { return ""; }
    }

    private string NormalizeQuestion(string value) {
      return Regex.Replace((value ?? "").ToLowerInvariant(), @"[^a-z0-9]+", " ").Trim();
    }

    private void SaveChat() {
      string value = Conversation.Text ?? "";
      if (value.Length > 10000) value = value.Substring(value.Length - 10000);
      data.Values["ChatHistory"] = value;
    }

    private string GetSelectedLanguage() {
      var item = LanguageBox.SelectedItem as ComboBoxItem;
      return item == null ? "HTML / JavaScript" : item.Content.ToString();
    }

    private string SlugForLanguage(string language) {
      return language.Contains("Python") ? "Python" : (language.Contains("C#") ? "CSharp" : (language.Contains("XAML") ? "Xaml" : "HtmlJs"));
    }

    private string ExtensionForLanguage(string language) {
      return language.Contains("Python") ? ".py" : (language.Contains("C#") ? ".cs" : (language.Contains("XAML") ? ".xaml" : ".html"));
    }

    private string TemplateFor(string language) {
      if (language.Contains("Python")) return "name = \"Pongo\"\r\nfor i in range(3):\r\n    print(\"Hello\", name, i)\r\n";
      if (language.Contains("C#")) return "using System;\r\n\r\nnamespace FunProgram {\r\n    class Program {\r\n        static void Main() {\r\n            Console.WriteLine(\"Hello from Pongo!\");\r\n        }\r\n    }\r\n}\r\n";
      if (language.Contains("XAML")) return "<Page\r\n    xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\"\r\n    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"\r\n    Background=\"#FF0B1020\">\r\n  <StackPanel Padding=\"20\">\r\n    <TextBlock Text=\"Hello from Pongo!\" FontSize=\"24\" />\r\n    <Button Content=\"Tap me\" />\r\n  </StackPanel>\r\n</Page>";
      return "<!doctype html>\r\n<html>\r\n<head><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><title>Pongo Offline App</title><style>body{font:18px sans-serif;background:#0b1020;color:#eef6ff;padding:20px}button{padding:12px;background:#238fe0;color:white;border:0;border-radius:8px}</style></head>\r\n<body><h1>Hello from Pongo!</h1><p>This tiny page runs without internet.</p><button onclick=\"document.getElementById('result').textContent='It works offline!'\">Test JavaScript</button><p id=\"result\"></p></body>\r\n</html>";
    }

    private async void Language_Changed(object sender, SelectionChangedEventArgs e) {
      if (!codeReady || CodeEditor == null || LanguageBox == null) return;
      try {
        await SaveSnippetAsync(currentLanguage, CodeEditor.Text);
        currentLanguage = GetSelectedLanguage();
        CodeEditor.Text = await LoadSnippetAsync(currentLanguage);
        CodeOutput.Text = "Selected " + currentLanguage + ". Save your work or press Run / Preview.";
        CodePreview.Visibility = Visibility.Collapsed;
      } catch { CodeStatus.Text = "Couldn't switch language; your current text is still visible."; }
    }

    private async Task SaveSnippetAsync(string language, string code) {
      try {
        StorageFile file = await ApplicationData.Current.LocalFolder.CreateFileAsync("PongoCode_" + SlugForLanguage(language) + ".txt", CreationCollisionOption.ReplaceExisting);
        await FileIO.WriteTextAsync(file, code ?? "");
      } catch { }
    }

    private async Task<string> LoadSnippetAsync(string language) {
      try {
        StorageFile file = await ApplicationData.Current.LocalFolder.GetFileAsync("PongoCode_" + SlugForLanguage(language) + ".txt");
        return await FileIO.ReadTextAsync(file);
      } catch { return TemplateFor(language); }
    }

    private async void Example_Click(object sender, RoutedEventArgs e) {
      CodeEditor.Text = TemplateFor(GetSelectedLanguage());
      await SaveSnippetAsync(GetSelectedLanguage(), CodeEditor.Text);
      CodeStatus.Text = "Loaded the " + GetSelectedLanguage() + " example. Edit it freely.";
    }

    private async void RunCode_Click(object sender, RoutedEventArgs e) {
      string language = GetSelectedLanguage();
      await SaveSnippetAsync(language, CodeEditor.Text);
      if (language.Contains("HTML / JavaScript")) {
        CodeOutput.Visibility = Visibility.Collapsed; CodePreview.Visibility = Visibility.Visible;
        CodePreview.NavigateToString(CodeEditor.Text);
        CodeStatus.Text = "HTML/JavaScript preview is running locally inside Pongo. No network is required.";
      } else {
        CodePreview.Visibility = Visibility.Collapsed; CodeOutput.Visibility = Visibility.Visible;
        CodeOutput.Text = language + " cannot execute in this phone app without its own runtime/compiler. Your source is saved locally; use Export App Project for a Windows app starter, or Save Code to export a source file.";
        CodeStatus.Text = "Offline source editor • " + language;
      }
    }

    private async void SaveCode_Click(object sender, RoutedEventArgs e) {
      try {
        var picker = new FileSavePicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.SuggestedFileName = "PongoExample";
        picker.FileTypeChoices.Add("Source code", new List<string> { ExtensionForLanguage(GetSelectedLanguage()) });
        StorageFile file = await picker.PickSaveFileAsync();
        if (file != null) {
          await FileIO.WriteTextAsync(file, CodeEditor.Text ?? "");
          await SaveSnippetAsync(GetSelectedLanguage(), CodeEditor.Text);
          CodeStatus.Text = "Saved source file: " + file.Name;
        } else CodeStatus.Text = "Save cancelled. Your draft remains on the phone.";
      } catch (Exception ex) { CodeStatus.Text = "Save failed: " + ex.Message; }
    }

    private async Task WriteProjectFileAsync(StorageFolder folder, string name, string contents) {
      StorageFile file = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);
      await FileIO.WriteTextAsync(file, contents);
    }

    private async void CreateAppProject_Click(object sender, RoutedEventArgs e) {
      try {
        CodeStatus.Text = "Choose where to create the starter project…";
        var picker = new FolderPicker();
        picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        picker.FileTypeFilter.Add("*");
        StorageFolder parent = await picker.PickSingleFolderAsync();
        if (parent == null) { CodeStatus.Text = "Project export cancelled; nothing changed."; return; }
        StorageFolder project = await parent.CreateFolderAsync("PongoStarterProject", CreationCollisionOption.GenerateUniqueName);
        StorageFolder assets = await project.CreateFolderAsync("Assets");
        string csproj = @"<Project ToolsVersion=""15.0"" DefaultTargets=""Build"" xmlns=""http://schemas.microsoft.com/developer/msbuild/2003"">
  <Import Project=""$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props"" Condition=""Exists('$(MSBuildExtensionsPath)\$(MSBuildToolsVersion)\Microsoft.Common.props')"" />
  <PropertyGroup><ProjectGuid>{1E7F80F1-2B0A-4F50-BB6A-2A4C77AC7123}</ProjectGuid><ProjectTypeGuids>{A5A43C5B-DE2A-4C0C-9213-0A381AF9435A};{FAE04EC0-301F-11D3-BF4B-00C04F79EFBC}</ProjectTypeGuids><OutputType>AppContainerExe</OutputType><RootNamespace>PongoStarter</RootNamespace><AssemblyName>PongoStarter</AssemblyName><DefaultLanguage>en-US</DefaultLanguage><TargetPlatformIdentifier>UAP</TargetPlatformIdentifier><TargetPlatformVersion>10.0.19041.0</TargetPlatformVersion><TargetPlatformMinVersion>10.0.14393.0</TargetPlatformMinVersion><MinimumVisualStudioVersion>14.0</MinimumVisualStudioVersion><AppxPackageSigningEnabled>false</AppxPackageSigningEnabled><AppxBundle>Never</AppxBundle><RestoreProjectStyle>PackageReference</RestoreProjectStyle></PropertyGroup>
  <PropertyGroup Condition=""'$(Configuration)|$(Platform)'=='Release|ARM'""><PlatformTarget>ARM</PlatformTarget><OutputPath>bin\ARM\Release\</OutputPath><Optimize>true</Optimize><DefineConstants>TRACE;NETFX_CORE;WINDOWS_UWP</DefineConstants></PropertyGroup>
  <PropertyGroup Condition=""'$(Configuration)|$(Platform)'=='Debug|ARM'""><PlatformTarget>ARM</PlatformTarget><OutputPath>bin\ARM\Debug\</OutputPath><DefineConstants>DEBUG;TRACE;NETFX_CORE;WINDOWS_UWP</DefineConstants></PropertyGroup>
  <ItemGroup><Compile Include=""App.xaml.cs""><DependentUpon>App.xaml</DependentUpon></Compile><Compile Include=""MainPage.xaml.cs""><DependentUpon>MainPage.xaml</DependentUpon></Compile></ItemGroup>
  <ItemGroup><ApplicationDefinition Include=""App.xaml""><Generator>MSBuild:Compile</Generator></ApplicationDefinition><Page Include=""MainPage.xaml""><Generator>MSBuild:Compile</Generator></Page></ItemGroup>
  <ItemGroup><Content Include=""Assets\StoreLogo.png""/><Content Include=""Assets\Square150x150Logo.png""/><Content Include=""Assets\Square44x44Logo.png""/><Content Include=""Assets\Wide310x150Logo.png""/><Content Include=""Assets\SplashScreen.png""/><Content Include=""UserCode.txt""/><AppxManifest Include=""Package.appxmanifest""/></ItemGroup>
  <ItemGroup><PackageReference Include=""Microsoft.NETCore.UniversalWindowsPlatform"" Version=""6.2.14"" /></ItemGroup>
  <Import Project=""$(MSBuildExtensionsPath)\Microsoft\WindowsXaml\v$(VisualStudioVersion)\Microsoft.Windows.UI.Xaml.CSharp.targets"" />
</Project>";
        string appxaml = @"<Application x:Class=""PongoStarter.App"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""><Application.Resources><ResourceDictionary/></Application.Resources></Application>";
        string appcs = @"using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
namespace PongoStarter {
  sealed partial class App : Application {
    public App() { InitializeComponent(); }
    protected override void OnLaunched(LaunchActivatedEventArgs e) {
      var frame = Window.Current.Content as Frame;
      if (frame == null) { frame = new Frame(); Window.Current.Content = frame; }
      if (frame.Content == null) frame.Navigate(typeof(MainPage), e.Arguments);
      Window.Current.Activate();
    }
  }
}";
        string pageXaml = @"<Page x:Class=""PongoStarter.MainPage"" xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation"" xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml"" Background=""#FF0B1020"">
  <Grid Padding=""20""><StackPanel><TextBlock Text=""My first app"" Foreground=""White"" FontSize=""28""/><TextBlock Text=""Built from Pongo Code Lab!"" Foreground=""#FF9DB1D1"" Margin=""0,8,0,20""/><Button Content=""Click me"" Click=""Button_Click""/><TextBlock x:Name=""Output"" Text=""It works."" Foreground=""White"" Margin=""0,12,0,0""/></StackPanel></Grid>
</Page>";
        string pageCs = @"using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
namespace PongoStarter {
  public sealed partial class MainPage : Page {
    public MainPage() { InitializeComponent(); }
    private int clicks = 0;
    private void Button_Click(object sender, RoutedEventArgs e) { clicks++; Output.Text = ""Button clicked "" + clicks + "" time(s).""; }
  }
}";
        string manifest = @"<?xml version=""1.0"" encoding=""utf-8""?>
<Package xmlns=""http://schemas.microsoft.com/appx/manifest/foundation/windows10"" xmlns:uap=""http://schemas.microsoft.com/appx/manifest/uap/windows10"" IgnorableNamespaces=""uap"">
  <Identity Name=""PongoCodeLab.Starter"" Publisher=""CN=PongoCodeLab"" Version=""1.0.0.0""/>
  <Properties><DisplayName>My Pongo Starter App</DisplayName><PublisherDisplayName>Pongo Code Lab</PublisherDisplayName><Logo>Assets\StoreLogo.png</Logo></Properties>
  <Dependencies><TargetDeviceFamily Name=""Windows.Universal"" MinVersion=""10.0.14393.0"" MaxVersionTested=""10.0.19041.0""/></Dependencies>
  <Resources><Resource Language=""en-us""/></Resources>
  <Applications><Application Id=""App"" Executable=""$targetnametoken$.exe"" EntryPoint=""PongoStarter.App""><uap:VisualElements DisplayName=""My Pongo Starter App"" Description=""A starter app created offline with Pongo Code Lab"" BackgroundColor=""#0B1020"" Square150x150Logo=""Assets\Square150x150Logo.png"" Square44x44Logo=""Assets\Square44x44Logo.png""><uap:DefaultTile Wide310x150Logo=""Assets\Wide310x150Logo.png""/><uap:SplashScreen Image=""Assets\SplashScreen.png""/></uap:VisualElements></Application></Applications>
</Package>";
        string solution = @"Microsoft Visual Studio Solution File, Format Version 12.00
# Visual Studio Version 17
VisualStudioVersion = 17.0.31903.59
MinimumVisualStudioVersion = 10.0.40219.1
Project(""{A5A43C5B-DE2A-4C0C-9213-0A381AF9435A}"") = ""PongoStarter"", ""PongoStarter.csproj"", ""{1E7F80F1-2B0A-4F50-BB6A-2A4C77AC7123}""
EndProject
Global
  GlobalSection(SolutionConfigurationPlatforms) = preSolution
    Debug|ARM = Debug|ARM
    Release|ARM = Release|ARM
  EndGlobalSection
  GlobalSection(ProjectConfigurationPlatforms) = postSolution
    {1E7F80F1-2B0A-4F50-BB6A-2A4C77AC7123}.Debug|ARM.ActiveCfg = Debug|ARM
    {1E7F80F1-2B0A-4F50-BB6A-2A4C77AC7123}.Debug|ARM.Build.0 = Debug|ARM
    {1E7F80F1-2B0A-4F50-BB6A-2A4C77AC7123}.Release|ARM.ActiveCfg = Release|ARM
    {1E7F80F1-2B0A-4F50-BB6A-2A4C77AC7123}.Release|ARM.Build.0 = Release|ARM
  EndGlobalSection
EndGlobal";
        await WriteProjectFileAsync(project, "PongoStarter.csproj", csproj);
        await WriteProjectFileAsync(project, "PongoStarter.sln", solution);
        await WriteProjectFileAsync(project, "App.xaml", appxaml);
        await WriteProjectFileAsync(project, "App.xaml.cs", appcs);
        await WriteProjectFileAsync(project, "MainPage.xaml", pageXaml);
        await WriteProjectFileAsync(project, "MainPage.xaml.cs", pageCs);
        await WriteProjectFileAsync(project, "Package.appxmanifest", manifest);
        await WriteProjectFileAsync(project, "UserCode.txt", "// Language: " + GetSelectedLanguage() + "\r\n// Created in Pongo Code Lab. Copy the code below into your app or script as appropriate.\r\n\r\n" + (CodeEditor.Text ?? ""));
        string readme = "PONGO CODE LAB — STARTER UWP APP\r\n\r\nThis folder contains source code, not a compiled .appx. A phone cannot compile/sign its own UWP app because the Windows SDK, MSBuild and MakeAppx toolchain are not installed on Windows 10 Mobile.\r\n\r\nBUILD ON A WINDOWS PC:\r\n1. Install Visual Studio with the Universal Windows Platform development workload and Windows 10 SDK 10.0.19041 or a compatible SDK.\r\n2. Open PongoStarter.sln.\r\n3. Restore NuGet packages.\r\n4. Select Release | ARM for Lumia 950.\r\n5. Build the project. For sideload installation, create/use a trusted signing certificate and sign the resulting package.\r\n6. Edit MainPage.xaml and MainPage.xaml.cs to build your app. Your current Code Lab snippet is saved in UserCode.txt.\r\n\r\nNo APIs or cloud service are required for this source export. Pongo's chat and editor remain offline.\r\n";
        await WriteProjectFileAsync(project, "README-BUILD.txt", readme);
        string[] assetsToCopy = new string[] { "StoreLogo.png", "Square150x150Logo.png", "Square44x44Logo.png", "Wide310x150Logo.png", "SplashScreen.png" };
        foreach (string assetName in assetsToCopy) {
          StorageFile asset = await StorageFile.GetFileFromApplicationUriAsync(new Uri("ms-appx:///Assets/" + assetName));
          await asset.CopyAsync(assets, assetName, NameCollisionOption.ReplaceExisting);
        }
        CodeStatus.Text = "Created " + project.Name + " with a UWP solution, C#/XAML files, manifest, assets, your snippet and build guide. Transfer it to a Windows PC to compile the .appx.";
      } catch (Exception ex) { CodeStatus.Text = "Project export failed: " + ex.Message; }
    }

    private async void Time_Click(object sender, RoutedEventArgs e) { await Submit("what time is it"); }
    private async void Notes_Click(object sender, RoutedEventArgs e) { await Submit("show my notes"); }
    private async void Search_Click(object sender, RoutedEventArgs e) {
      string query = Prompt.Text.Trim();
      if (query.Length == 0) query = "latest technology news";
      Prompt.Text = "";
      await Launcher.LaunchUriAsync(new Uri("https://www.bing.com/search?q=" + Uri.EscapeDataString(query)));
      Status.Text = "Opened browser search. The local AI did not use an API.";
    }
  }
}