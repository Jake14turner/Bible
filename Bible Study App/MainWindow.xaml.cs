using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Bible_Study_App;

public partial class MainWindow : Window
{
    private const int DwmwaBorderColor = 34;
    private const int DwmwaCaptionColor = 35;
    private const int DwmwaTextColor = 36;
    private const string BackendApiUrl = "http://localhost:5055";
    private const string DefaultOverviewPrompt = "Summarize the most important insight from the available study notes and scripture, connect them when both are present, and give one clear next step.";
    private static readonly HttpClient BackendHttpClient = new();
    private static readonly Dictionary<string, int[]> BibleVerseCounts = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Genesis"] = [31, 25, 24, 26, 32, 22, 24, 22, 29, 32, 32, 20, 18, 24, 21, 16, 27, 33, 38, 18, 34, 24, 20, 67, 34, 35, 46, 22, 35, 43, 55, 32, 20, 31, 29, 43, 36, 30, 23, 23, 57, 38, 34, 34, 28, 34, 31, 22, 33, 26],
        ["Exodus"] = [22, 25, 22, 31, 23, 30, 25, 32, 35, 29, 10, 51, 22, 31, 27, 36, 16, 27, 25, 26, 36, 31, 33, 18, 40, 37, 21, 43, 46, 38, 18, 35, 23, 35, 35, 38, 29, 31, 43, 38],
        ["Leviticus"] = [17, 16, 17, 35, 19, 30, 38, 36, 24, 20, 47, 8, 59, 57, 33, 34, 16, 30, 37, 27, 24, 33, 44, 23, 55, 46, 34],
        ["Numbers"] = [54, 34, 51, 49, 31, 27, 89, 26, 23, 36, 35, 16, 33, 45, 41, 50, 13, 32, 22, 29, 35, 41, 30, 25, 18, 65, 23, 31, 40, 16, 54, 42, 56, 29, 34, 13],
        ["Deuteronomy"] = [46, 37, 29, 49, 33, 25, 26, 20, 29, 22, 32, 32, 18, 29, 23, 22, 20, 22, 21, 20, 23, 30, 25, 22, 19, 19, 26, 68, 29, 20, 30, 52, 29, 12],
        ["Joshua"] = [18, 24, 17, 24, 15, 27, 26, 35, 27, 43, 23, 24, 33, 15, 63, 10, 18, 28, 51, 9, 45, 34, 16, 33],
        ["Judges"] = [36, 23, 31, 24, 31, 40, 25, 35, 57, 18, 40, 15, 25, 20, 20, 31, 13, 31, 30, 48, 25],
        ["Ruth"] = [22, 23, 18, 22],
        ["1 Samuel"] = [28, 36, 21, 22, 12, 21, 17, 22, 27, 27, 15, 25, 23, 52, 35, 23, 58, 30, 24, 42, 15, 23, 29, 22, 44, 25, 12, 25, 11, 31, 13],
        ["2 Samuel"] = [27, 32, 39, 12, 25, 23, 29, 18, 13, 19, 27, 31, 39, 33, 37, 23, 29, 33, 43, 26, 22, 51, 39, 25],
        ["1 Kings"] = [53, 46, 28, 34, 18, 38, 51, 66, 28, 29, 43, 33, 34, 31, 34, 34, 24, 46, 21, 43, 29, 53],
        ["2 Kings"] = [18, 25, 27, 44, 27, 33, 20, 29, 37, 36, 21, 21, 25, 29, 38, 20, 41, 37, 37, 21, 26, 20, 37, 20, 30],
        ["1 Chronicles"] = [54, 55, 24, 43, 26, 81, 40, 40, 44, 14, 47, 40, 14, 17, 29, 43, 27, 17, 19, 8, 30, 19, 32, 31, 31, 32, 34, 21, 30],
        ["2 Chronicles"] = [17, 18, 17, 22, 14, 42, 22, 18, 31, 19, 23, 16, 22, 15, 19, 14, 19, 34, 11, 37, 20, 12, 21, 27, 28, 23, 9, 27, 36, 27, 21, 33, 25, 33, 27, 23],
        ["Ezra"] = [11, 70, 13, 24, 17, 22, 28, 36, 15, 44],
        ["Nehemiah"] = [11, 20, 32, 23, 19, 19, 73, 18, 38, 39, 36, 47, 31],
        ["Esther"] = [22, 23, 15, 17, 14, 14, 10, 17, 32, 3],
        ["Job"] = [22, 13, 26, 21, 27, 30, 21, 22, 35, 22, 20, 25, 28, 22, 35, 22, 16, 21, 29, 29, 34, 30, 17, 25, 6, 14, 23, 28, 25, 31, 40, 22, 33, 37, 16, 33, 24, 41, 30, 24, 34, 17],
        ["Psalms"] = [6, 12, 8, 8, 12, 10, 17, 9, 20, 18, 7, 8, 6, 7, 5, 11, 15, 50, 14, 9, 13, 31, 6, 10, 22, 12, 14, 9, 11, 12, 24, 11, 22, 22, 28, 12, 40, 22, 13, 17, 13, 11, 5, 26, 17, 11, 9, 14, 20, 23, 19, 9, 6, 7, 23, 13, 11, 11, 17, 12, 8, 12, 11, 10, 13, 20, 7, 35, 36, 5, 24, 20, 28, 23, 10, 12, 20, 72, 13, 19, 16, 8, 18, 12, 13, 17, 7, 18, 52, 17, 16, 15, 5, 23, 11, 13, 12, 9, 9, 5, 8, 28, 22, 35, 45, 48, 43, 13, 31, 7, 10, 10, 9, 8, 18, 19, 2, 29, 176, 7, 8, 9, 4, 8, 5, 6, 5, 6, 8, 8, 3, 18, 3, 3, 21, 26, 9, 8, 24, 13, 10, 7, 12, 15, 21, 10, 20, 14, 9, 6],
        ["Proverbs"] = [33, 22, 35, 27, 23, 35, 27, 36, 18, 32, 31, 28, 25, 35, 33, 33, 28, 24, 29, 30, 31, 29, 35, 34, 28, 28, 27, 28, 27, 33, 31],
        ["Ecclesiastes"] = [18, 26, 22, 16, 20, 12, 29, 17, 18, 20, 10, 14],
        ["Song of Solomon"] = [17, 17, 11, 16, 16, 13, 13, 14],
        ["Isaiah"] = [31, 22, 26, 6, 30, 13, 25, 22, 21, 34, 16, 6, 22, 32, 9, 14, 14, 7, 25, 6, 17, 25, 18, 23, 12, 21, 13, 29, 24, 33, 9, 20, 24, 17, 10, 22, 38, 22, 8, 31, 29, 25, 28, 28, 25, 13, 15, 22, 26, 11, 23, 15, 12, 17, 13, 12, 21, 14, 21, 22, 11, 12, 19, 12, 25, 24],
        ["Jeremiah"] = [19, 37, 25, 31, 31, 30, 34, 22, 26, 25, 23, 17, 27, 22, 21, 21, 27, 23, 15, 18, 14, 30, 40, 10, 38, 24, 22, 17, 32, 24, 40, 44, 26, 22, 19, 32, 21, 28, 18, 16, 18, 22, 13, 30, 5, 28, 7, 47, 39, 46, 64, 34],
        ["Lamentations"] = [22, 22, 66, 22, 22],
        ["Ezekiel"] = [28, 10, 27, 17, 17, 14, 27, 18, 11, 22, 25, 28, 23, 23, 8, 63, 24, 32, 14, 49, 32, 31, 49, 27, 17, 21, 36, 26, 21, 26, 18, 32, 33, 31, 15, 38, 28, 23, 29, 49, 26, 20, 27, 31, 25, 24, 23, 35],
        ["Daniel"] = [21, 49, 30, 37, 31, 28, 28, 27, 27, 21, 45, 13],
        ["Hosea"] = [11, 23, 5, 19, 15, 11, 16, 14, 17, 15, 12, 14, 16, 9],
        ["Joel"] = [20, 32, 21],
        ["Amos"] = [15, 16, 15, 13, 27, 14, 17, 14, 15],
        ["Obadiah"] = [21],
        ["Jonah"] = [17, 10, 10, 11],
        ["Micah"] = [16, 13, 12, 13, 15, 16, 20],
        ["Nahum"] = [15, 13, 19],
        ["Habakkuk"] = [17, 20, 19],
        ["Zephaniah"] = [18, 15, 20],
        ["Haggai"] = [15, 23],
        ["Zechariah"] = [21, 13, 10, 14, 11, 15, 14, 23, 17, 12, 17, 14, 9, 21],
        ["Malachi"] = [14, 17, 18, 6],
        ["Matthew"] = [25, 23, 17, 25, 48, 34, 29, 34, 38, 42, 30, 50, 58, 36, 39, 28, 27, 35, 30, 34, 46, 46, 39, 51, 46, 75, 66, 20],
        ["Mark"] = [45, 28, 35, 41, 43, 56, 37, 38, 50, 52, 33, 44, 37, 72, 47, 20],
        ["Luke"] = [80, 52, 38, 44, 39, 49, 50, 56, 62, 42, 54, 59, 35, 35, 32, 31, 37, 43, 48, 47, 38, 71, 56, 53],
        ["John"] = [51, 25, 36, 54, 47, 71, 53, 59, 41, 42, 57, 50, 38, 31, 27, 33, 26, 40, 42, 31, 25],
        ["Acts"] = [26, 47, 26, 37, 42, 15, 60, 40, 43, 48, 30, 25, 52, 28, 41, 40, 34, 28, 41, 38, 40, 30, 35, 27, 27, 32, 44, 31],
        ["Romans"] = [32, 29, 31, 25, 21, 23, 25, 39, 33, 21, 36, 21, 14, 23, 33, 27],
        ["1 Corinthians"] = [31, 16, 23, 21, 13, 20, 40, 13, 27, 33, 34, 31, 13, 40, 58, 24],
        ["2 Corinthians"] = [24, 17, 18, 18, 21, 18, 16, 24, 15, 18, 33, 21, 14],
        ["Galatians"] = [24, 21, 29, 31, 26, 18],
        ["Ephesians"] = [23, 22, 21, 32, 33, 24],
        ["Philippians"] = [30, 30, 21, 23],
        ["Colossians"] = [29, 23, 25, 18],
        ["1 Thessalonians"] = [10, 20, 13, 18, 28],
        ["2 Thessalonians"] = [12, 17, 18],
        ["1 Timothy"] = [20, 15, 16, 16, 25, 21],
        ["2 Timothy"] = [18, 26, 17, 22],
        ["Titus"] = [16, 15, 15],
        ["Philemon"] = [25],
        ["Hebrews"] = [14, 18, 19, 16, 14, 20, 28, 13, 28, 39, 40, 29, 25],
        ["James"] = [27, 26, 18, 17, 20],
        ["1 Peter"] = [25, 25, 22, 19, 14],
        ["2 Peter"] = [21, 22, 18],
        ["1 John"] = [10, 29, 24, 21, 21],
        ["2 John"] = [13],
        ["3 John"] = [14],
        ["Jude"] = [25],
        ["Revelation"] = [20, 29, 22, 11, 14, 17, 17, 13, 21, 11, 19, 17, 18, 20, 8, 21, 18, 24, 21, 15, 27, 21]
    };
    private readonly Dictionary<string, WorkspaceItem> _workspaceByBook = new();
    private readonly Dictionary<string, ExtraPanelRuntime> _extraPanelRuntimes = new();
    private readonly Dictionary<TextBox, TextAnnotationAdorner> _textAnnotationAdorners = new();
    private readonly ObservableCollection<EditorCommandOption> _filteredSlashCommands = new();
    private readonly Brush[] _accentPalette;
    private readonly List<EditorCommandOption> _slashCommands;
    private readonly DispatcherTimer _slashCommandRefreshTimer;
    private readonly DispatcherTimer _workspaceSaveTimer;
    private readonly DispatcherTimer _toastTimer;
    private readonly Dictionary<ScrollViewer, DispatcherTimer> _scrollBarRevealTimers = new();
    private readonly string _workspaceFilePath;
    private readonly ObservableCollection<DailyStudyEntry> _todayEditedStudies = new();
    private readonly ObservableCollection<ScheduledNotificationDisplay> _scheduledNotificationDisplays = new();
    private readonly ObservableCollection<ReminderScheduleItem> _reminderScheduleItems = new();
    private readonly ObservableCollection<ReminderPreset> _reminderPresets = new();
    private readonly ObservableCollection<ColorSchemePreset> _colorSchemePresets = new();
    private readonly ObservableCollection<ScriptureMemoryState> _scriptureMemoryItems = new();
    private readonly ObservableCollection<string> _availableAiModels = new();
    private readonly ObservableCollection<ScriptureVerseDisplay> _scriptureVerses = new();
    private readonly List<ScheduledNotificationState> _scheduledNotifications = new();
    private readonly Dictionary<string, DailyNoteState> _dailyNotesByDate = new();
    private readonly Dictionary<string, Dictionary<string, List<string>>> _scriptureTextByBookChapter = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, Paragraph> _scriptureParagraphsByVerse = new();
    private readonly Dictionary<ScriptureReferenceKey, List<TagntWordEntry>> _tagntEntriesByReference = new();
    private readonly Dictionary<string, GreekLexiconEntry> _greekLexiconByStrong = new(StringComparer.OrdinalIgnoreCase);
    private static readonly GreekLexiconEntry EmptyGreekLexiconEntry = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    private enum DefinitionLineKind { Lead, Main, Sub }
    private sealed record DefinitionLine(DefinitionLineKind Kind, string Marker, string Text);
    private sealed record VerseReference(string DisplayText, string BookName, int Chapter, int Verse);
    private sealed record SelectionSegment(Control Target, string SourceKey, int Start, int Length, string Text);
    private enum MovablePanelKind { Editor, Scripture, Strongs }
    private enum PanelPreset { Top, Left, Right, Bottom, Float }
    private static readonly Brush PanelPresetIdleBrush = BrushFrom("#E6DDAA");
    private static readonly Brush PanelPresetIdleBorderBrush = BrushFrom("#111111");
    private static readonly Brush PanelPresetActiveBrush = BrushFrom("#B7C98B");
    private static readonly Brush PanelPresetActiveBorderBrush = BrushFrom("#606C38");
    private string _scriptureTranslationLabel = "ASV";
    private string? _scriptureVisibleBookName;
    private int? _scriptureVisibleChapter;
    private BibleBook? _selectedBook;
    private WorkspaceItem? _currentContainer;
    private WorkspaceItem? _currentStudy;
    private StudyBlock? _activeStudyBlock;
    private TextBox? _activeStudyTextBox;
    private string _selectedColorSchemeName = "Olive Study";
    private AppNavTab _activeNavTab = AppNavTab.BibleStudy;
    private string _lastSlashQuery = string.Empty;
    private double _bibleBooksSmoothScrollTargetOffset;
    private DateTime _bibleBooksSmoothScrollLastFrame;
    private bool _bibleBooksSmoothScrollActive;
    private double _scriptureSmoothScrollTargetOffset;
    private DateTime _scriptureSmoothScrollLastFrame;
    private bool _scriptureSmoothScrollActive;
    private double _studyEditorSmoothScrollTargetOffset;
    private DateTime _studyEditorSmoothScrollLastFrame;
    private bool _studyEditorSmoothScrollActive;
    private double _todaySmoothScrollTargetOffset;
    private DateTime _todaySmoothScrollLastFrame;
    private bool _todaySmoothScrollActive;
    private double _scheduledMessagesSmoothScrollTargetOffset;
    private DateTime _scheduledMessagesSmoothScrollLastFrame;
    private bool _scheduledMessagesSmoothScrollActive;
    private double _strongsSmoothScrollTargetOffset;
    private DateTime _strongsSmoothScrollLastFrame;
    private bool _strongsSmoothScrollActive;
    private bool _maybeInSlashCommand;
    private bool _isApplyingEditorCommand;
    private bool _isPageTransitioning;
    private bool _isLoadingPassageSelection;
    private bool _isLoadingDailyDetails;
    private bool _isLoadingReminderSettings;
    private bool _isInitializing = true;
    private bool _isRestoringStudyPanelGeometry;
    private bool _sidebarCollapsed;
    private bool _scripturePanelHidden;
    private bool _strongsEnabled;
    private bool _greekLexiconLoaded;
    private bool _isRefreshingAiModels;
    private bool _suppressRenameCommit;
    private GridLength _scripturePanelVisibleWidth = new(440);
    private GridLength _strongsPanelVisibleWidth = new(330);
    private string _selectedAiModel = "llama3.2:latest";
    private MovablePanelKind? _draggingPanelKind;
    private TranslateTransform? _draggingPanelTransform;
    private UIElement? _draggingPanelHandle;
    private Point _panelDragStartPoint;
    private Point _panelDragStartOffset;
    private Point _panelDragPointer;
    private Point _panelDragPendingOffset;
    private bool _panelDragRenderSubscribed;
    private double _panelDragOriginalOpacity = 1;
    private PanelPreset? _hoveredPanelPreset;
    private string? _hoveredPanelPresetCommand;
    private bool _panelPresetTrayOpen;
    private MovablePanelKind? _resizingPanelKind;
    private Rect _panelResizeStartBounds;
    private Point _panelResizeStartPointer;
    private Vector _panelResizeAccumulatedDelta;
    private Rect _panelResizePendingBounds;
    private CacheMode? _panelResizeOriginalCacheMode;
    private bool _panelResizeRenderSubscribed;
    private ExtraPanelRuntime? _draggingExtraPanel;
    private ExtraPanelRuntime? _resizingExtraPanel;
    private Point _extraPanelDragStartPoint;
    private Point _extraPanelDragStartOffset;
    private Rect _extraPanelResizeStartBounds;
    private Point _extraPanelResizeStartPointer;
    private PanelPreset _scripturePanelPreset = PanelPreset.Right;
    private PanelPreset _strongsPanelPreset = PanelPreset.Right;
    private int _topPanelZIndex = 130;
    private Control? _selectionTarget;
    private string _selectionSourceKey = string.Empty;
    private int _selectionStart;
    private int _selectionLength;
    private string _selectionText = string.Empty;
    private TextRange? _selectedRichTextRange;
    private readonly DispatcherTimer _savedNoteCloseTimer;
    private TextAnnotationState? _savedNoteAnnotation;
    private Control? _savedNoteTarget;
    private bool _isEditingSavedNote;
    private readonly List<SelectionSegment> _selectionSegments = new();
    private readonly List<TextAnnotationState> _multiBlockSelectionPreview = new();
    private TextBox? _multiSelectStartTextBox;
    private int _multiSelectStartIndex;
    private TextBox? _multiSelectEndTextBox;
    private int _multiSelectEndIndex;
    private bool _isMultiBlockDragging;
    private bool _isClosingSelectionUi;
    private bool _suppressSelectionSubmenuClose;
    private IReadOnlyList<TextAnnotationState> _pendingNoteAnnotations = [];
    private ScriptureMemoryState? _activeMemoryPassage;
    private int _activeMemorySectionIndex;
    private string _memoryTypedText = string.Empty;
    private readonly List<bool> _memoryTypedErrors = new();
    private bool _memoryShowMoreWords;
    private TextPointer? _memoryCaretPointer;
    private bool _memorySessionComplete;
    private int _memorySectionGeneration;
    private bool _isUpdatingMemoryRange;

    public ObservableCollection<BibleBook> BibleBooks { get; } = new();
    public ObservableCollection<BibleBook> OldTestamentBooks { get; } = new();
    public ObservableCollection<BibleBook> NewTestamentBooks { get; } = new();

    public MainWindow()
    {
        InitializeComponent();
        _savedNoteCloseTimer = new DispatcherTimer(DispatcherPriority.Input)
        {
            Interval = TimeSpan.FromMilliseconds(260)
        };
        _savedNoteCloseTimer.Tick += (_, _) =>
        {
            _savedNoteCloseTimer.Stop();
            if (!_isEditingSavedNote && !SavedNoteCard.IsMouseOver)
            {
                SavedNotePopup.IsOpen = false;
            }
        };
        SourceInitialized += (_, _) => UpdateNativeTitleBarColors();
        Deactivated += (_, _) => CloseSelectionUi();
        Application.Current.Exit += (_, _) => SaveWorkspaceState();

        AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(StudyBlocks_PreviewMouseDown), true);
        AddHandler(Mouse.PreviewMouseMoveEvent, new MouseEventHandler(StudyBlocks_PreviewMouseMove), true);
        AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(StudyBlocks_PreviewMouseUp), true);

        _accentPalette =
        [
            BrushFrom("#DDA15E"),
            BrushFrom("#BC6C25"),
            BrushFrom("#FEFAE0"),
            BrushFrom("#A98467"),
            BrushFrom("#606C38"),
            BrushFrom("#D6C99D"),
            BrushFrom("#F1E7C5"),
            BrushFrom("#4F5A2F")
        ];

        _slashCommands =
        [
            new EditorCommandOption("Text", "Plain paragraph", "T", "text normal paragraph", EditorBlockKind.Normal, PickEditorBrush("TextSecondary")),
            new EditorCommandOption("Heading 1", "Large section heading", "H1", "h1 heading title", EditorBlockKind.HeadingOne, PickEditorBrush("Gold")),
            new EditorCommandOption("Heading 2", "Subsection heading", "H2", "h2 heading subtitle", EditorBlockKind.HeadingTwo, PickEditorBrush("Sky")),
            new EditorCommandOption("Heading 3", "Small section heading", "H3", "h3 heading subtitle small", EditorBlockKind.HeadingThree, PickEditorBrush("Violet")),
            new EditorCommandOption("Quote", "Scripture, commentary, or cited text", "Q", "q quote quo citation blockquote", EditorBlockKind.Quote, PickEditorBrush("Mint")),
            new EditorCommandOption("Bulleted List", "Simple list item", "-", "bullet bulleted list unordered", EditorBlockKind.BulletedList, PickEditorBrush("Coral")),
            new EditorCommandOption("Numbered List", "Ordered list item", "1.", "number numbered list ordered", EditorBlockKind.NumberedList, PickEditorBrush("Gold")),
            new EditorCommandOption("To-do", "Checkbox-style action item", "[ ]", "todo to-do task checkbox check", EditorBlockKind.Todo, PickEditorBrush("Mint")),
            new EditorCommandOption("Callout", "Highlighted note block", "!", "callout note highlight reminder", EditorBlockKind.Callout, PickEditorBrush("Sky")),
            new EditorCommandOption("Code", "Monospace study snippet", "</>", "code mono monospace snippet", EditorBlockKind.Code, PickEditorBrush("Violet"))
        ];
        _slashCommandRefreshTimer = new DispatcherTimer(DispatcherPriority.ContextIdle)
        {
            Interval = TimeSpan.FromMilliseconds(12)
        };
        _slashCommandRefreshTimer.Tick += SlashCommandRefreshTimer_Tick;
        _workspaceSaveTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(450)
        };
        _workspaceSaveTimer.Tick += WorkspaceSaveTimer_Tick;
        _toastTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(2.4)
        };
        _toastTimer.Tick += ToastTimer_Tick;
        _workspaceFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Bible Study Studio",
            "workspace.json");
        LoadBibleBooks();
        LoadDefaultColorSchemes();
        OldTestamentBooksItems.ItemsSource = OldTestamentBooks;
        NewTestamentBooksItems.ItemsSource = NewTestamentBooks;
        SlashCommandList.ItemsSource = _filteredSlashCommands;
        TodayEditedItems.ItemsSource = _todayEditedStudies;
        ScheduledNotificationItems.ItemsSource = _scheduledNotificationDisplays;
        ReminderScheduleItems.ItemsSource = _reminderScheduleItems;
        ReminderPresetSelect.ItemsSource = _reminderPresets;
        ColorSchemePresetItems.ItemsSource = _colorSchemePresets;
        AiModelComboBox.ItemsSource = _availableAiModels;
        MemoryPassageItems.ItemsSource = _scriptureMemoryItems;
        LoadWorkspaceState();
        LoadScriptureText();
        InitializeMemorySelectors();
        LoadTagntData();
        WorkspaceCountText.Text = $"{BibleBooks.Count} books ready";
        RenderBreadcrumbs();
        RenderTodayDashboard();
        SetActiveNavTab(AppNavTab.BibleStudy);
        _isInitializing = false;
        LocationChanged += WindowPlacement_Changed;
        SizeChanged += WindowPlacement_Changed;
        StateChanged += WindowPlacement_Changed;
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        CommitLivePanelStateForShutdown();
        StopBibleBooksSmoothScroll();
        StopScriptureSmoothScroll();
        StopStudyEditorSmoothScroll();
        StopTodaySmoothScroll();
        StopScheduledMessagesSmoothScroll();
        StopStrongsSmoothScroll();
        StopPanelDragRendering();
        StopScrollBarRevealTimers();
        SaveWorkspaceState();
        base.OnClosing(e);
    }

    private void CommitLivePanelStateForShutdown()
    {
        if (_draggingPanelKind is not null && _draggingPanelTransform is not null)
        {
            var offset = ClampPanelOffset(_draggingPanelKind.Value, _panelDragPendingOffset);
            _draggingPanelTransform.BeginAnimation(TranslateTransform.XProperty, null);
            _draggingPanelTransform.BeginAnimation(TranslateTransform.YProperty, null);
            _draggingPanelTransform.X = offset.X;
            _draggingPanelTransform.Y = offset.Y;
        }

        if (_resizingPanelKind is not null)
        {
            ApplyPendingPanelResize(updateAvoidance: false);
        }

        SnapshotCurrentStudyPanelGeometry();
        foreach (var runtime in _extraPanelRuntimes.Values)
        {
            runtime.State.Geometry = new PanelGeometryState
            {
                X = Math.Round(runtime.Transform.X, 2),
                Y = Math.Round(runtime.Transform.Y, 2),
                Width = Math.Round(runtime.Root.ActualWidth > 0 ? runtime.Root.ActualWidth : runtime.Root.Width, 2),
                Height = Math.Round(runtime.Root.ActualHeight > 0 ? runtime.Root.ActualHeight : runtime.Root.Height, 2)
            };
        }
    }

    private void LoadBibleBooks()
    {
        AddOldTestamentBooks();
        AddNewTestamentBooks();
    }

    private void AddOldTestamentBooks()
    {
        AddBook("Genesis", "Old Testament", 50);
        AddBook("Exodus", "Old Testament", 40);
        AddBook("Leviticus", "Old Testament", 27);
        AddBook("Numbers", "Old Testament", 36);
        AddBook("Deuteronomy", "Old Testament", 34);
        AddBook("Joshua", "Old Testament", 24);
        AddBook("Judges", "Old Testament", 21);
        AddBook("Ruth", "Old Testament", 4);
        AddBook("1 Samuel", "Old Testament", 31);
        AddBook("2 Samuel", "Old Testament", 24);
        AddBook("1 Kings", "Old Testament", 22);
        AddBook("2 Kings", "Old Testament", 25);
        AddBook("1 Chronicles", "Old Testament", 29);
        AddBook("2 Chronicles", "Old Testament", 36);
        AddBook("Ezra", "Old Testament", 10);
        AddBook("Nehemiah", "Old Testament", 13);
        AddBook("Esther", "Old Testament", 10);
        AddBook("Job", "Old Testament", 42);
        AddBook("Psalms", "Old Testament", 150);
        AddBook("Proverbs", "Old Testament", 31);
        AddBook("Ecclesiastes", "Old Testament", 12);
        AddBook("Song of Solomon", "Old Testament", 8);
        AddBook("Isaiah", "Old Testament", 66);
        AddBook("Jeremiah", "Old Testament", 52);
        AddBook("Lamentations", "Old Testament", 5);
        AddBook("Ezekiel", "Old Testament", 48);
        AddBook("Daniel", "Old Testament", 12);
        AddBook("Hosea", "Old Testament", 14);
        AddBook("Joel", "Old Testament", 3);
        AddBook("Amos", "Old Testament", 9);
        AddBook("Obadiah", "Old Testament", 1);
        AddBook("Jonah", "Old Testament", 4);
        AddBook("Micah", "Old Testament", 7);
        AddBook("Nahum", "Old Testament", 3);
        AddBook("Habakkuk", "Old Testament", 3);
        AddBook("Zephaniah", "Old Testament", 3);
        AddBook("Haggai", "Old Testament", 2);
        AddBook("Zechariah", "Old Testament", 14);
        AddBook("Malachi", "Old Testament", 4);
    }

    private void AddNewTestamentBooks()
    {
        AddBook("Matthew", "New Testament", 28);
        AddBook("Mark", "New Testament", 16);
        AddBook("Luke", "New Testament", 24);
        AddBook("John", "New Testament", 21);
        AddBook("Acts", "New Testament", 28);
        AddBook("Romans", "New Testament", 16);
        AddBook("1 Corinthians", "New Testament", 16);
        AddBook("2 Corinthians", "New Testament", 13);
        AddBook("Galatians", "New Testament", 6);
        AddBook("Ephesians", "New Testament", 6);
        AddBook("Philippians", "New Testament", 4);
        AddBook("Colossians", "New Testament", 4);
        AddBook("1 Thessalonians", "New Testament", 5);
        AddBook("2 Thessalonians", "New Testament", 3);
        AddBook("1 Timothy", "New Testament", 6);
        AddBook("2 Timothy", "New Testament", 4);
        AddBook("Titus", "New Testament", 3);
        AddBook("Philemon", "New Testament", 1);
        AddBook("Hebrews", "New Testament", 13);
        AddBook("James", "New Testament", 5);
        AddBook("1 Peter", "New Testament", 5);
        AddBook("2 Peter", "New Testament", 3);
        AddBook("1 John", "New Testament", 5);
        AddBook("2 John", "New Testament", 1);
        AddBook("3 John", "New Testament", 1);
        AddBook("Jude", "New Testament", 1);
        AddBook("Revelation", "New Testament", 22);
    }

    private void AddBook(string name, string testament, int chapters)
    {
        if (!BibleVerseCounts.TryGetValue(name, out var verseCounts))
        {
            verseCounts = Enumerable.Repeat(1, chapters).ToArray();
        }

        BibleBooks.Add(new BibleBook(
            name,
            testament,
            verseCounts,
            _accentPalette[BibleBooks.Count % _accentPalette.Length]));
        var book = BibleBooks[^1];
        if (string.Equals(testament, "Old Testament", StringComparison.OrdinalIgnoreCase))
        {
            OldTestamentBooks.Add(book);
        }
        else
        {
            NewTestamentBooks.Add(book);
        }
    }

    private void LoadWorkspaceState()
    {
        if (!File.Exists(_workspaceFilePath))
        {
            EnsureDefaultReminderSettings();
            LoadColorSettings(null);
            return;
        }

        try
        {
            var json = File.ReadAllText(_workspaceFilePath);
            var state = JsonSerializer.Deserialize<WorkspaceState>(json);
            if (state is null)
            {
                return;
            }

            RestoreWindowPlacement(state.WindowPlacement);
            _workspaceByBook.Clear();
            _dailyNotesByDate.Clear();
            _scheduledNotifications.Clear();
            foreach (var bookState in state.Books)
            {
                var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == bookState.Name);
                var fallbackBrush = book?.AccentBrush ?? PickEditorBrush("Mint");
                var workspace = CreateWorkspaceItemFromState(bookState, null, fallbackBrush);
                _workspaceByBook[workspace.Name] = workspace;
            }

            foreach (var dayState in state.DailyNotes)
            {
                if (!string.IsNullOrWhiteSpace(dayState.DateKey))
                {
                    _dailyNotesByDate[dayState.DateKey] = dayState;
                }
            }

            _scheduledNotifications.AddRange(state.ScheduledNotifications
                .Where(notification => !string.IsNullOrWhiteSpace(notification.SequenceId)));
            foreach (var memoryItem in state.ScriptureMemoryPassages)
            {
                memoryItem.NormalizeLegacyRange();
                _scriptureMemoryItems.Add(memoryItem);
            }

            OldTestamentBooksExpander.IsExpanded = state.OldTestamentBooksExpanded;
            NewTestamentBooksExpander.IsExpanded = state.NewTestamentBooksExpanded;
            LoadReminderSettings(state.ReminderSettings);
            LoadColorSettings(state.ColorSettings);
        }
        catch
        {
            _workspaceByBook.Clear();
            _dailyNotesByDate.Clear();
            _scheduledNotifications.Clear();
            _reminderScheduleItems.Clear();
            _reminderPresets.Clear();
            EnsureDefaultReminderSettings();
            LoadDefaultColorSchemes();
            LoadColorSettings(null);
        }
    }

    private WorkspaceItem CreateWorkspaceItemFromState(WorkspaceItemState state, WorkspaceItem? parent, Brush fallbackBrush)
    {
        var accentBrush = string.IsNullOrWhiteSpace(state.AccentHex)
            ? fallbackBrush
            : BrushFrom(state.AccentHex);
        var item = new WorkspaceItem(state.Name, state.Kind, accentBrush, parent);
        item.PassageStartChapter = state.PassageStartChapter;
        item.PassageStartVerse = state.PassageStartVerse;
        item.PassageEndChapter = state.PassageEndChapter;
        item.PassageEndVerse = state.PassageEndVerse;
        item.LastEditedAt = state.LastEditedAt;
        item.EditorPanelGeometry = state.EditorPanelGeometry;
        item.ScripturePanelGeometry = state.ScripturePanelGeometry;
        item.StrongsPanelGeometry = state.StrongsPanelGeometry;
        foreach (var annotation in state.TextAnnotations)
        {
            item.TextAnnotations.Add(annotation);
        }
        item.ExtraPanels.Clear();
        foreach (var extraPanel in state.ExtraPanels)
        {
            item.ExtraPanels.Add(extraPanel);
        }

        foreach (var blockState in state.Blocks)
        {
            item.Blocks.Add(CreateStudyBlockFromState(blockState));
        }

        foreach (var childState in state.Children)
        {
            item.Children.Add(CreateWorkspaceItemFromState(childState, item, accentBrush));
        }

        return item;
    }

    private StudyBlock CreateStudyBlockFromState(StudyBlockState state)
    {
        var block = CreateStudyBlock(state.Kind);
        block.Text = state.Text;
        block.IsChecked = state.IsChecked;
        return block;
    }

    private void WorkspaceSaveTimer_Tick(object? sender, EventArgs e)
    {
        _workspaceSaveTimer.Stop();
        SaveWorkspaceState();
    }

    private void QueueWorkspaceSave()
    {
        if (_workspaceSaveTimer is null)
        {
            return;
        }

        _workspaceSaveTimer.Stop();
        _workspaceSaveTimer.Start();
    }

    private void WindowPlacement_Changed(object? sender, EventArgs e)
    {
        if (!_isInitializing)
        {
            QueueWorkspaceSave();
        }
    }

    private WindowPlacementState CreateWindowPlacementState()
    {
        var bounds = WindowState == WindowState.Normal
            ? new Rect(Left, Top, ActualWidth > 0 ? ActualWidth : Width, ActualHeight > 0 ? ActualHeight : Height)
            : RestoreBounds;

        return new WindowPlacementState
        {
            Left = bounds.Left,
            Top = bounds.Top,
            Width = bounds.Width,
            Height = bounds.Height,
            IsMaximized = WindowState == WindowState.Maximized
        };
    }

    private void RestoreWindowPlacement(WindowPlacementState? placement)
    {
        if (placement is null ||
            !double.IsFinite(placement.Left) ||
            !double.IsFinite(placement.Top) ||
            !double.IsFinite(placement.Width) ||
            !double.IsFinite(placement.Height) ||
            placement.Width < MinWidth ||
            placement.Height < MinHeight)
        {
            return;
        }

        const double minimumVisiblePixels = 96;
        var virtualLeft = SystemParameters.VirtualScreenLeft;
        var virtualTop = SystemParameters.VirtualScreenTop;
        var virtualRight = virtualLeft + SystemParameters.VirtualScreenWidth;
        var virtualBottom = virtualTop + SystemParameters.VirtualScreenHeight;
        var width = Math.Min(placement.Width, SystemParameters.VirtualScreenWidth);
        var height = Math.Min(placement.Height, SystemParameters.VirtualScreenHeight);
        var left = Math.Clamp(placement.Left, virtualLeft - width + minimumVisiblePixels, virtualRight - minimumVisiblePixels);
        var top = Math.Clamp(placement.Top, virtualTop, virtualBottom - minimumVisiblePixels);

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = width;
        Height = height;
        WindowState = placement.IsMaximized ? WindowState.Maximized : WindowState.Normal;
    }

    private void ShowToast(string message)
    {
        if (_isInitializing || ToastHost is null || ToastText is null || ToastHostTransform is null)
        {
            return;
        }

        ToastText.Text = message;
        ToastHost.Visibility = Visibility.Visible;
        ToastHost.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        ToastHostTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(28, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        ToastHostTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(-8, 0, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    private void ToastTimer_Tick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        HideToast();
    }

    private void HideToast()
    {
        if (ToastHost is null || ToastHostTransform is null)
        {
            return;
        }

        var fade = new DoubleAnimation(ToastHost.Opacity, 0, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => ToastHost.Visibility = Visibility.Collapsed;
        ToastHost.BeginAnimation(OpacityProperty, fade);
        ToastHostTransform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, 22, TimeSpan.FromMilliseconds(180))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        });
    }

    private void SaveWorkspaceState()
    {
        if (_workspaceSaveTimer is null)
        {
            return;
        }

        _workspaceSaveTimer.Stop();
        SnapshotCurrentStudyPanelGeometry();

        try
        {
            var directory = Path.GetDirectoryName(_workspaceFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var state = new WorkspaceState
            {
                Books = _workspaceByBook.Values
                    .Select(CreateWorkspaceItemState)
                    .ToList(),
                DailyNotes = _dailyNotesByDate.Values
                    .OrderByDescending(day => day.DateKey)
                    .ToList(),
                ScheduledNotifications = _scheduledNotifications
                    .OrderByDescending(notification => notification.CreatedAt)
                    .ToList(),
                ScriptureMemoryPassages = _scriptureMemoryItems.ToList(),
                OldTestamentBooksExpanded = OldTestamentBooksExpander.IsExpanded,
                NewTestamentBooksExpanded = NewTestamentBooksExpander.IsExpanded,
                ReminderSettings = CreateReminderSettingsState(),
                ColorSettings = CreateColorSettingsState(),
                WindowPlacement = CreateWindowPlacementState()
            };
            var json = JsonSerializer.Serialize(state, new JsonSerializerOptions
            {
                WriteIndented = true
            });
            var tempPath = $"{_workspaceFilePath}.tmp";
            using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None,
                       bufferSize: 4096, FileOptions.WriteThrough))
            using (var writer = new StreamWriter(stream, leaveOpen: true))
            {
                writer.Write(json);
                writer.Flush();
                stream.Flush(flushToDisk: true);
            }
            File.Move(tempPath, _workspaceFilePath, true);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Workspace save failed: {ex}");
        }
    }

    private void TestamentBooksExpander_StateChanged(object sender, RoutedEventArgs e)
    {
        if (sender is Expander expander)
        {
            AnimateTestamentExpander(expander, expander.IsExpanded);
        }

        if (_isInitializing)
        {
            return;
        }

        QueueWorkspaceSave();
    }

    private void TestamentBooksExpander_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is not Expander expander)
        {
            return;
        }

        SetTestamentExpanderState(expander, expander.IsExpanded);
    }

    private void SetTestamentExpanderState(Expander expander, bool isExpanded)
    {
        if (expander.Template.FindName("ExpandSite", expander) is not Border site
            || expander.Template.FindName("ExpandSiteScale", expander) is not ScaleTransform scale)
        {
            return;
        }

        site.Opacity = isExpanded ? 1 : 0;
        scale.ScaleY = isExpanded ? 1 : 0;
    }

    private void AnimateTestamentExpander(Expander expander, bool isExpanded)
    {
        if (expander.Template.FindName("ExpandSite", expander) is not Border site
            || expander.Template.FindName("ExpandSiteScale", expander) is not ScaleTransform scale)
        {
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var duration = TimeSpan.FromMilliseconds(240);
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(
            scale.ScaleY,
            isExpanded ? 1 : 0,
            duration)
        {
            EasingFunction = ease
        });
        site.BeginAnimation(OpacityProperty, new DoubleAnimation(
            site.Opacity,
            isExpanded ? 1 : 0,
            TimeSpan.FromMilliseconds(isExpanded ? 200 : 150))
        {
            EasingFunction = ease
        });
    }

    private WorkspaceItemState CreateWorkspaceItemState(WorkspaceItem item)
    {
        return new WorkspaceItemState
        {
            Name = item.Name,
            Kind = item.Kind,
            AccentHex = BrushToHex(item.AccentBrush),
            PassageStartChapter = item.PassageStartChapter,
            PassageStartVerse = item.PassageStartVerse,
            PassageEndChapter = item.PassageEndChapter,
            PassageEndVerse = item.PassageEndVerse,
            LastEditedAt = item.LastEditedAt,
            EditorPanelGeometry = item.EditorPanelGeometry,
            ScripturePanelGeometry = item.ScripturePanelGeometry,
            StrongsPanelGeometry = item.StrongsPanelGeometry,
            TextAnnotations = item.TextAnnotations.ToList(),
            ExtraPanels = item.ExtraPanels.ToList(),
            Children = item.Children
                .Select(CreateWorkspaceItemState)
                .ToList(),
            Blocks = item.Blocks
                .Select(CreateStudyBlockState)
                .ToList()
        };
    }

    private static StudyBlockState CreateStudyBlockState(StudyBlock block)
    {
        return new StudyBlockState
        {
            Kind = block.Kind,
            Text = block.Text,
            IsChecked = block.IsChecked
        };
    }

    private void LoadReminderSettings(ReminderSettingsState? settings)
    {
        _isLoadingReminderSettings = true;
        _reminderScheduleItems.Clear();
        _reminderPresets.Clear();

        var source = settings ?? CreateDefaultReminderSettingsState();
        _selectedAiModel = string.IsNullOrWhiteSpace(source.AiModel)
            ? "llama3.2:latest"
            : source.AiModel.Trim();
        OverviewNotificationEnabledCheckBox.IsChecked = source.OverviewEnabled;
        OverviewPromptTextBox.Text = string.IsNullOrWhiteSpace(source.OverviewPrompt)
            ? DefaultOverviewPrompt
            : source.OverviewPrompt.Trim();
        RemindersEnabledCheckBox.IsChecked = source.RemindersEnabled;

        foreach (var reminder in source.Reminders)
        {
            AddReminderScheduleItem(CreateReminderScheduleItem(reminder));
        }

        foreach (var preset in source.Presets)
        {
            _reminderPresets.Add(new ReminderPreset(
                preset.Name,
                preset.Reminders.Select(CreateReminderScheduleItem).ToList()));
        }

        if (_reminderScheduleItems.Count == 0)
        {
            foreach (var reminder in CreateDefaultReminderSettingsState().Reminders)
            {
                AddReminderScheduleItem(CreateReminderScheduleItem(reminder));
            }
        }

        if (_reminderPresets.Count == 0)
        {
            _reminderPresets.Add(CreateBuiltInReminderPreset());
        }

        ReminderPresetSelect.SelectedIndex = _reminderPresets.Count > 0 ? 0 : -1;
        UpdateReminderCount();
        _isLoadingReminderSettings = false;
    }

    private void EnsureDefaultReminderSettings()
    {
        LoadReminderSettings(CreateDefaultReminderSettingsState());
    }

    private ReminderSettingsState CreateReminderSettingsState()
    {
        return new ReminderSettingsState
        {
            AiModel = _selectedAiModel,
            OverviewEnabled = OverviewNotificationEnabledCheckBox.IsChecked == true,
            OverviewPrompt = string.IsNullOrWhiteSpace(OverviewPromptTextBox.Text)
                ? DefaultOverviewPrompt
                : OverviewPromptTextBox.Text.Trim(),
            RemindersEnabled = RemindersEnabledCheckBox.IsChecked == true,
            Reminders = _reminderScheduleItems
                .Select(CreateReminderScheduleItemState)
                .ToList(),
            Presets = _reminderPresets
                .Select(preset => new ReminderPresetState
                {
                    Name = preset.Name,
                    Reminders = preset.Reminders
                        .Select(CreateReminderScheduleItemState)
                        .ToList()
                })
                .ToList()
        };
    }

    private static ReminderSettingsState CreateDefaultReminderSettingsState()
    {
        return new ReminderSettingsState
        {
            AiModel = "llama3.2:latest",
            OverviewEnabled = true,
            OverviewPrompt = DefaultOverviewPrompt,
            RemindersEnabled = true,
            Reminders =
            [
                new ReminderScheduleItemState { DelayAmount = 2, DelayUnit = "hours", Prompt = "Help me pause and reconnect with the passage." },
                new ReminderScheduleItemState { DelayAmount = 5, DelayUnit = "hours", Prompt = "Encourage me to practice one truth from this study before evening." },
                new ReminderScheduleItemState { DelayAmount = 9, DelayUnit = "hours", Prompt = "Help me review what stood out and end the day with one reflection." }
            ],
            Presets =
            [
                new ReminderPresetState
                {
                    Name = "Classic Study Day",
                    Reminders =
                    [
                        new ReminderScheduleItemState { DelayAmount = 2, DelayUnit = "hours", Prompt = "Help me pause and reconnect with the passage." },
                        new ReminderScheduleItemState { DelayAmount = 5, DelayUnit = "hours", Prompt = "Encourage me to practice one truth from this study before evening." },
                        new ReminderScheduleItemState { DelayAmount = 9, DelayUnit = "hours", Prompt = "Help me review what stood out and end the day with one reflection." }
                    ]
                }
            ]
        };
    }

    private void LoadDefaultColorSchemes()
    {
        _colorSchemePresets.Clear();
        _colorSchemePresets.Add(new ColorSchemePreset("Olive Study", "#283618", "#1F2A14", "#606C38", "#DDA15E", "#FEFAE0", "#F1E7C5", true));
        _colorSchemePresets.Add(new ColorSchemePreset("Forest Light", "#1E2B20", "#172318", "#4E7042", "#B8D58A", "#F3F8E8", "#DDE8C7", true));
        _colorSchemePresets.Add(new ColorSchemePreset("Deep Walnut", "#2B2118", "#1F1712", "#694B35", "#D9A05F", "#FFF4DF", "#E7D2B5", true));
    }

    private void LoadColorSettings(ColorSettingsState? settings)
    {
        if (settings is not null)
        {
            LoadDefaultColorSchemes();
            foreach (var preset in settings.Presets.Where(preset => !preset.BuiltIn))
            {
                _colorSchemePresets.Add(preset);
            }
        }

        var selected = _colorSchemePresets.FirstOrDefault(preset =>
                string.Equals(preset.Name, settings?.SelectedSchemeName, StringComparison.OrdinalIgnoreCase))
            ?? _colorSchemePresets.FirstOrDefault();
        if (selected is null)
        {
            return;
        }

        ApplyColorScheme(selected);
    }

    private ColorSettingsState CreateColorSettingsState()
    {
        return new ColorSettingsState
        {
            SelectedSchemeName = _selectedColorSchemeName,
            Presets = _colorSchemePresets.ToList()
        };
    }

    private void ApplyColorScheme(ColorSchemePreset preset)
    {
        _selectedColorSchemeName = preset.Name;
        SetBrushColor("AppBackground", preset.AppBackground);
        SetBrushColor("SidebarBackground", preset.SidebarBackground);
        SetBrushColor("PanelBackground", preset.PanelBackground);
        SetBrushColor("Mint", preset.Accent);
        SetBrushColor("Gold", preset.Accent);
        SetBrushColor("TextPrimary", preset.TextPrimary);
        SetBrushColor("Sky", preset.TextPrimary);
        SetBrushColor("TextSecondary", preset.TextSecondary);
        SetBrushColor("TextMuted", preset.TextSecondary);
        SetBrushColor("StrokeSoft", preset.AppBackground);
        SetBrushColor("Coral", preset.Accent);
        UpdateNativeTitleBarColors(preset);
        RefreshStudyBlockThemeBrushes();
    }

    private void UpdateNativeTitleBarColors(ColorSchemePreset? preset = null)
    {
        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        var selected = preset ?? _colorSchemePresets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, _selectedColorSchemeName, StringComparison.OrdinalIgnoreCase));
        if (selected is null
            || !TryParseColor(selected.AppBackground, out var captionColor)
            || !TryParseColor(selected.TextPrimary, out var textColor)
            || !TryParseColor(selected.Accent, out var borderColor))
        {
            return;
        }

        var caption = ToColorRef(captionColor);
        var text = ToColorRef(textColor);
        var border = ToColorRef(borderColor);
        _ = DwmSetWindowAttribute(handle, DwmwaCaptionColor, ref caption, sizeof(uint));
        _ = DwmSetWindowAttribute(handle, DwmwaTextColor, ref text, sizeof(uint));
        _ = DwmSetWindowAttribute(handle, DwmwaBorderColor, ref border, sizeof(uint));
    }

    private static uint ToColorRef(Color color)
    {
        return (uint)(color.R | color.G << 8 | color.B << 16);
    }

    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr windowHandle, int attribute, ref uint value, int valueSize);

    private void SetBrushColor(string resourceName, string hex)
    {
        if (TryParseColor(hex, out var color))
        {
            Resources[resourceName] = new SolidColorBrush(color);
        }
    }

    private static bool TryParseColor(string value, out Color color)
    {
        try
        {
            color = (Color)ColorConverter.ConvertFromString(value.Trim());
            return true;
        }
        catch
        {
            color = Colors.Transparent;
            return false;
        }
    }

    private ReminderPreset CreateBuiltInReminderPreset()
    {
        return new ReminderPreset(
            "Classic Study Day",
            CreateDefaultReminderSettingsState().Reminders.Select(CreateReminderScheduleItem).ToList());
    }

    private ReminderScheduleItem CreateReminderScheduleItem(ReminderScheduleItemState state)
    {
        var item = new ReminderScheduleItem(
            Math.Max(1, state.DelayAmount),
            NormalizeReminderUnit(state.DelayUnit),
            string.IsNullOrWhiteSpace(state.Prompt)
                ? "Return to {passage}."
                : state.Prompt.Trim());
        item.PropertyChanged += ReminderScheduleItem_PropertyChanged;
        return item;
    }

    private void AddReminderScheduleItem(ReminderScheduleItem item, bool insertFirst = false)
    {
        item.PropertyChanged -= ReminderScheduleItem_PropertyChanged;
        item.PropertyChanged += ReminderScheduleItem_PropertyChanged;
        if (insertFirst)
        {
            _reminderScheduleItems.Insert(0, item);
        }
        else
        {
            _reminderScheduleItems.Add(item);
        }

        UpdateReminderCount();
    }

    private static ReminderScheduleItemState CreateReminderScheduleItemState(ReminderScheduleItem item)
    {
        return new ReminderScheduleItemState
        {
            DelayAmount = Math.Max(1, item.DelayAmount),
            DelayUnit = NormalizeReminderUnit(item.DelayUnit),
            Prompt = item.Prompt.Trim()
        };
    }

    private static string NormalizeReminderUnit(string? unit)
    {
        return unit?.Trim().ToLowerInvariant() switch
        {
            "minute" or "minutes" => "minutes",
            "day" or "days" => "days",
            _ => "hours"
        };
    }

    private void ReminderScheduleItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_isLoadingReminderSettings)
        {
            return;
        }

        QueueWorkspaceSave();
    }

    private static string BrushToHex(Brush brush)
    {
        return brush is SolidColorBrush solidColorBrush
            ? solidColorBrush.Color.ToString()
            : "#FFFFFFFF";
    }

    private void RunPageTransition(object? source, Action switchPage)
    {
        CloseSelectionUi();
        if (_isPageTransitioning || MainContentRoot is null || PageTransitionOverlay is null || PageTransitionCircle is null)
        {
            switchPage();
            return;
        }

        _isPageTransitioning = true;

        var origin = new Point(MainContentRoot.ActualWidth / 2, MainContentRoot.ActualHeight / 2);
        if (source is FrameworkElement element)
        {
            try
            {
                origin = element.TranslatePoint(new Point(element.ActualWidth / 2, element.ActualHeight / 2), MainContentRoot);
            }
            catch
            {
                origin = new Point(MainContentRoot.ActualWidth / 2, MainContentRoot.ActualHeight / 2);
            }
        }

        var maxX = Math.Max(Math.Abs(origin.X), Math.Abs(MainContentRoot.ActualWidth - origin.X));
        var maxY = Math.Max(Math.Abs(origin.Y), Math.Abs(MainContentRoot.ActualHeight - origin.Y));
        var finalDiameter = Math.Sqrt((maxX * maxX) + (maxY * maxY)) * 2.35;

        PageTransitionOverlay.Visibility = Visibility.Visible;
        PageTransitionOverlay.Opacity = 1;
        PageTransitionCircle.Width = 0;
        PageTransitionCircle.Height = 0;
        Canvas.SetLeft(PageTransitionCircle, origin.X);
        Canvas.SetTop(PageTransitionCircle, origin.Y);

        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
        var growDuration = TimeSpan.FromMilliseconds(200);
        var growWidth = new DoubleAnimation(0, finalDiameter, growDuration) { EasingFunction = ease };
        var growHeight = new DoubleAnimation(0, finalDiameter, growDuration) { EasingFunction = ease };
        var moveLeft = new DoubleAnimation(origin.X, origin.X - finalDiameter / 2, growDuration) { EasingFunction = ease };
        var moveTop = new DoubleAnimation(origin.Y, origin.Y - finalDiameter / 2, growDuration) { EasingFunction = ease };

        growWidth.Completed += (_, _) =>
        {
            switchPage();

            var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(100))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            };
            fade.Completed += (_, _) =>
            {
                PageTransitionOverlay.Visibility = Visibility.Collapsed;
                PageTransitionOverlay.Opacity = 1;
                PageTransitionCircle.BeginAnimation(WidthProperty, null);
                PageTransitionCircle.BeginAnimation(HeightProperty, null);
                PageTransitionOverlay.BeginAnimation(OpacityProperty, null);
                _isPageTransitioning = false;
            };
            PageTransitionOverlay.BeginAnimation(OpacityProperty, fade);
        };

        PageTransitionCircle.BeginAnimation(WidthProperty, growWidth);
        PageTransitionCircle.BeginAnimation(HeightProperty, growHeight);
        PageTransitionCircle.BeginAnimation(Canvas.LeftProperty, moveLeft);
        PageTransitionCircle.BeginAnimation(Canvas.TopProperty, moveTop);
    }

    private void BibleBook_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BibleBook book })
        {
            return;
        }

        RunPageTransition(sender, () => OpenBook(book));
    }

    private void BibleStudyNav_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, NavigateBibleStudyNav);
    }

    private void NavigateBibleStudyNav()
    {
        if (_selectedBook is null)
        {
            BackToLibrary();
            return;
        }

        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        WorkspacePanel.Visibility = Visibility.Visible;
        StudyPanel.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.BibleStudy);
        RenderWorkspace();
        RenderBreadcrumbs();
    }

    private void TodayDashboard_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowTodayDashboard);
    }

    private void ScriptureMemoryNav_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowScriptureMemory);
    }

    private void ShowScriptureMemory()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        ScriptureMemoryPanel.Visibility = Visibility.Visible;
        MemoryLibraryView.Visibility = Visibility.Visible;
        MemoryPracticeView.Visibility = Visibility.Collapsed;
        MainHeading.Text = "Scripture Memory";
        MainSubheading.Text = "Learn the words, then let the cues fall away.";
        MainSubheading.Visibility = Visibility.Visible;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.ScriptureMemory);
        BreadcrumbTrail.Children.Clear();
        RefreshMemoryLibrary();
    }

    private void InitializeMemorySelectors()
    {
        MemoryModalBookSelect.ItemsSource = BibleBooks;
        MemoryModalBookSelect.SelectedItem = BibleBooks.FirstOrDefault(book => book.Name == "Genesis") ?? BibleBooks.FirstOrDefault();
        RefreshMemoryLibrary();
    }

    private void MemoryOpenPassageModal_Click(object sender, RoutedEventArgs e)
    {
        var genesis = BibleBooks.FirstOrDefault(book => book.Name == "Genesis") ?? BibleBooks.FirstOrDefault();
        MemoryModalBookSelect.SelectedItem = genesis;
        ConfigureMemoryModalForBook(genesis);
        MemoryPassageModalOverlay.Visibility = Visibility.Visible;
        MemoryPassageModalOverlay.Opacity = 0;
        MemoryPassageModalScale.ScaleX = 0.94;
        MemoryPassageModalScale.ScaleY = 0.94;
        MemoryPassageModalTranslate.Y = 10;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        MemoryPassageModalOverlay.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(170)) { EasingFunction = ease });
        MemoryPassageModalScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        MemoryPassageModalScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(0.94, 1, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        MemoryPassageModalTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(10, 0, TimeSpan.FromMilliseconds(220)) { EasingFunction = ease });
        MemoryPassageModalOverlay.Focus();
    }

    private void MemoryClosePassageModal_Click(object sender, RoutedEventArgs e) => CloseMemoryPassageModal();

    private void MemoryPassageModalOverlay_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (ReferenceEquals(e.OriginalSource, MemoryPassageModalOverlay))
        {
            CloseMemoryPassageModal();
        }
    }

    private void MemoryPassageModalOverlay_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            CloseMemoryPassageModal();
            e.Handled = true;
        }
    }

    private void CloseMemoryPassageModal()
    {
        if (MemoryPassageModalOverlay.Visibility != Visibility.Visible)
        {
            return;
        }
        var fade = new DoubleAnimation(MemoryPassageModalOverlay.Opacity, 0, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };
        fade.Completed += (_, _) => MemoryPassageModalOverlay.Visibility = Visibility.Collapsed;
        MemoryPassageModalOverlay.BeginAnimation(OpacityProperty, fade);
    }

    private void MemoryModalBookSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingMemoryRange || MemoryModalBookSelect.SelectedItem is not BibleBook book)
        {
            return;
        }
        ConfigureMemoryModalForBook(book);
    }

    private void ConfigureMemoryModalForBook(BibleBook? book)
    {
        if (book is null)
        {
            return;
        }
        _isUpdatingMemoryRange = true;
        var chapters = Enumerable.Range(1, book.Chapters).ToList();
        MemoryFromChapterSelect.ItemsSource = chapters;
        MemoryThroughChapterSelect.ItemsSource = chapters;
        MemoryFromChapterSelect.SelectedItem = 1;
        MemoryThroughChapterSelect.SelectedItem = 1;
        SetMemoryVerseOptions(MemoryFromVerseSelect, book, 1, 1);
        SetMemoryVerseOptions(MemoryThroughVerseSelect, book, 1, 3);
        _isUpdatingMemoryRange = false;
        UpdateMemoryModalPassagePreview();
    }

    private static void SetMemoryVerseOptions(ComboBox selector, BibleBook book, int chapter, int selectedVerse)
    {
        var count = chapter >= 1 && chapter <= book.VerseCounts.Length ? book.VerseCounts[chapter - 1] : 1;
        selector.ItemsSource = Enumerable.Range(1, count).ToList();
        selector.SelectedItem = Math.Clamp(selectedVerse, 1, count);
    }

    private void MemoryFromChapterSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingMemoryRange || MemoryModalBookSelect.SelectedItem is not BibleBook book
            || MemoryFromChapterSelect.SelectedItem is not int chapter)
        {
            return;
        }
        _isUpdatingMemoryRange = true;
        SetMemoryVerseOptions(MemoryFromVerseSelect, book, chapter, 1);
        if (MemoryThroughChapterSelect.SelectedItem is not int throughChapter || throughChapter < chapter)
        {
            MemoryThroughChapterSelect.SelectedItem = chapter;
            SetMemoryVerseOptions(MemoryThroughVerseSelect, book, chapter, 1);
        }
        _isUpdatingMemoryRange = false;
        UpdateMemoryModalPassagePreview();
    }

    private void MemoryThroughChapterSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isUpdatingMemoryRange || MemoryModalBookSelect.SelectedItem is not BibleBook book
            || MemoryThroughChapterSelect.SelectedItem is not int chapter)
        {
            return;
        }
        _isUpdatingMemoryRange = true;
        if (MemoryFromChapterSelect.SelectedItem is int fromChapter && chapter < fromChapter)
        {
            chapter = fromChapter;
            MemoryThroughChapterSelect.SelectedItem = chapter;
        }
        SetMemoryVerseOptions(MemoryThroughVerseSelect, book, chapter, 1);
        _isUpdatingMemoryRange = false;
        UpdateMemoryModalPassagePreview();
    }

    private void MemoryModalRange_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isUpdatingMemoryRange)
        {
            UpdateMemoryModalPassagePreview();
        }
    }

    private void UpdateMemoryModalPassagePreview()
    {
        if (!TryGetMemoryModalRange(out var book, out var startChapter, out var startVerse, out var endChapter, out var endVerse))
        {
            MemoryModalPassagePreview.Text = string.Empty;
            return;
        }
        if (startChapter == endChapter && endVerse < startVerse)
        {
            endVerse = startVerse;
            _isUpdatingMemoryRange = true;
            MemoryThroughVerseSelect.SelectedItem = endVerse;
            _isUpdatingMemoryRange = false;
        }
        MemoryModalPassagePreview.Text = $"From {book.Name} {startChapter}:{startVerse} through {book.Name} {endChapter}:{endVerse}";
    }

    private bool TryGetMemoryModalRange(out BibleBook book, out int startChapter, out int startVerse,
        out int endChapter, out int endVerse)
    {
        book = MemoryModalBookSelect.SelectedItem as BibleBook ?? BibleBooks[0];
        startChapter = MemoryFromChapterSelect.SelectedItem as int? ?? 0;
        startVerse = MemoryFromVerseSelect.SelectedItem as int? ?? 0;
        endChapter = MemoryThroughChapterSelect.SelectedItem as int? ?? 0;
        endVerse = MemoryThroughVerseSelect.SelectedItem as int? ?? 0;
        return startChapter > 0 && startVerse > 0 && endChapter > 0 && endVerse > 0;
    }

    private void MemoryAddPassage_Click(object sender, RoutedEventArgs e)
    {
        if (!TryGetMemoryModalRange(out var book, out var startChapter, out var startVerse,
                out var endChapter, out var endVerse))
        {
            ShowToast("Choose a passage first");
            return;
        }

        if (_scriptureMemoryItems.Any(item => item.BookName == book.Name
                                             && item.StartChapter == startChapter && item.StartVerse == startVerse
                                             && item.EndChapter == endChapter && item.EndVerse == endVerse))
        {
            ShowToast("That passage is already in memory");
            return;
        }

        var item = new ScriptureMemoryState
        {
            BookName = book.Name,
            Chapter = startChapter,
            StartChapter = startChapter,
            StartVerse = startVerse,
            EndChapter = endChapter,
            EndVerse = endVerse,
            Translation = _scriptureTranslationLabel,
            CreatedAt = DateTimeOffset.Now
        };
        for (var chapter = startChapter; chapter <= endChapter; chapter++)
        {
            if (!TryGetChapterVerses(book.Name, chapter, out var verses))
            {
                continue;
            }
            var firstVerse = chapter == startChapter ? startVerse : 1;
            var lastVerse = chapter == endChapter ? Math.Min(endVerse, verses.Count) : verses.Count;
            for (var verse = firstVerse; verse <= lastVerse; verse++)
            {
                item.Sections.Add(new ScriptureMemorySectionState
                {
                    Chapter = chapter,
                    Verse = verse,
                    Text = verses[verse - 1].Trim()
                });
            }
        }

        if (item.Sections.Count == 0)
        {
            ShowToast("That passage could not be loaded");
            return;
        }

        _scriptureMemoryItems.Insert(0, item);
        CloseMemoryPassageModal();
        QueueWorkspaceSave();
        RefreshMemoryLibrary();
        AnimateMemoryCardArrival();
        ShowToast($"{item.Reference} added");
    }

    private void AnimateMemoryCardArrival()
    {
        MemoryPassageItems.Opacity = 0.55;
        MemoryPassageItems.BeginAnimation(OpacityProperty, new DoubleAnimation(0.55, 1, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void RefreshMemoryLibrary()
    {
        MemoryEmptyState.Visibility = _scriptureMemoryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        MemoryPassageItems.Items.Refresh();
    }

    private void MemoryRemovePassage_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ScriptureMemoryState item })
        {
            return;
        }
        _scriptureMemoryItems.Remove(item);
        QueueWorkspaceSave();
        RefreshMemoryLibrary();
        ShowToast("Memory passage removed");
    }

    private void MemoryPractice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: ScriptureMemoryState item })
        {
            BeginMemoryPractice(item);
        }
    }

    private void BeginMemoryPractice(ScriptureMemoryState item)
    {
        if (item.Sections.Count == 0)
        {
            return;
        }
        _activeMemoryPassage = item;
        _activeMemorySectionIndex = 0;
        _memorySessionComplete = false;
        MemoryLibraryView.Visibility = Visibility.Collapsed;
        MemoryPracticeView.Visibility = Visibility.Visible;
        StartMemorySection(0, animate: true);
        Dispatcher.BeginInvoke(() => MemoryPracticeView.Focus(), DispatcherPriority.Input);
    }

    private void StartMemorySection(int sectionIndex, bool animate)
    {
        if (_activeMemoryPassage is null || sectionIndex < 0 || sectionIndex >= _activeMemoryPassage.Sections.Count)
        {
            return;
        }
        _activeMemorySectionIndex = sectionIndex;
        _memorySectionGeneration++;
        _memoryTypedText = string.Empty;
        _memoryTypedErrors.Clear();
        _memoryShowMoreWords = false;
        _memorySessionComplete = false;
        var section = _activeMemoryPassage.Sections[sectionIndex];
        MemoryPracticeReference.Text = $"{_activeMemoryPassage.BookName} {section.Chapter}:{section.Verse}";
        MemoryPracticeStage.Text = $"{ScriptureMemoryState.GetStageLabel(section.CueLevel)} · {_activeMemoryPassage.Translation}";
        MemoryPracticeSectionProgress.Text = $"{sectionIndex + 1} / {_activeMemoryPassage.Sections.Count}";
        MemoryPracticeFeedback.Text = "Start typing anywhere";
        RenderMemoryPracticeText(animateCaret: false);
        if (animate)
        {
            MemoryPracticeTextView.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(240))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
            });
        }
    }

    private void MemoryPracticeBack_Click(object sender, RoutedEventArgs e)
    {
        _activeMemoryPassage = null;
        MemoryPracticeView.Visibility = Visibility.Collapsed;
        MemoryLibraryView.Visibility = Visibility.Visible;
        RefreshMemoryLibrary();
    }

    private void MemoryPracticeView_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        MemoryPracticeView.Focus();
    }

    private void MemoryPracticeView_PreviewTextInput(object sender, TextCompositionEventArgs e)
    {
        if (_activeMemoryPassage is null || _memorySessionComplete || string.IsNullOrEmpty(e.Text))
        {
            return;
        }
        var target = CurrentMemorySection.Text;
        foreach (var inputCharacter in e.Text)
        {
            if (_memoryTypedText.Length >= target.Length || char.IsControl(inputCharacter))
            {
                continue;
            }
            var expected = target[_memoryTypedText.Length];
            _memoryTypedText += inputCharacter;
            _memoryTypedErrors.Add(char.ToUpperInvariant(inputCharacter) != char.ToUpperInvariant(expected));
        }
        RenderMemoryPracticeText(animateCaret: true);
        if (_memoryTypedText.Length == target.Length)
        {
            CompleteMemorySection();
        }
        e.Handled = true;
    }

    private void MemoryPracticeView_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (_activeMemoryPassage is null)
        {
            return;
        }
        if (e.Key == Key.Back && _memoryTypedText.Length > 0 && !_memorySessionComplete)
        {
            _memoryTypedText = _memoryTypedText[..^1];
            _memoryTypedErrors.RemoveAt(_memoryTypedErrors.Count - 1);
            RenderMemoryPracticeText(animateCaret: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Enter && _memorySessionComplete)
        {
            StartMemorySection(0, animate: true);
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            MemoryPracticeBack_Click(this, new RoutedEventArgs());
            e.Handled = true;
        }
    }

    private ScriptureMemorySectionState CurrentMemorySection => _activeMemoryPassage!.Sections[_activeMemorySectionIndex];

    private void RenderMemoryPracticeText(bool animateCaret)
    {
        if (_activeMemoryPassage is null)
        {
            return;
        }
        var section = CurrentMemorySection;
        var target = section.Text;
        var wordIndexes = BuildMemoryWordIndexes(target);
        var effectiveLevel = Math.Max(0, section.CueLevel - (_memoryShowMoreWords ? 1 : 0));
        MemoryPracticeParagraph.Inlines.Clear();
        _memoryCaretPointer = null;

        for (var index = 0; index < target.Length; index++)
        {
            var typed = index < _memoryTypedText.Length;
            var isError = typed && _memoryTypedErrors[index];
            var wordIndex = wordIndexes[index];
            var cueVisible = char.IsWhiteSpace(target[index]) || ShouldShowMemoryWord(_activeMemoryPassage.Id,
                _activeMemorySectionIndex, wordIndex, effectiveLevel);
            var displayCharacter = typed ? _memoryTypedText[index] : target[index];
            var run = new Run(displayCharacter.ToString())
            {
                Foreground = typed
                    ? (isError ? GetResourceBrush("Coral") : GetResourceBrush("TextPrimary"))
                    : (cueVisible ? GetResourceBrush("TextMuted") : Brushes.Transparent),
                FontWeight = typed ? FontWeights.SemiBold : FontWeights.Normal
            };
            MemoryPracticeParagraph.Inlines.Add(run);
            if (index == _memoryTypedText.Length)
            {
                _memoryCaretPointer = run.ContentStart;
            }
        }
        _memoryCaretPointer ??= MemoryPracticeParagraph.ContentEnd;

        var progress = target.Length == 0 ? 0 : (double)_memoryTypedText.Length / target.Length;
        var targetWidth = 340 * progress;
        MemoryTypingProgress.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(targetWidth, TimeSpan.FromMilliseconds(120))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        var mistakes = _memoryTypedErrors.Count(value => value);
        MemoryPracticeFeedback.Text = _memoryTypedText.Length == 0
            ? (_memoryShowMoreWords ? "More cues shown for this attempt" : "Start typing anywhere")
            : mistakes == 0 ? "Clean so far" : $"{mistakes} {(mistakes == 1 ? "mistake" : "mistakes")} · backspace to correct";
        Dispatcher.BeginInvoke(() => UpdateMemorySmoothCaret(animateCaret), DispatcherPriority.Render);
    }

    private static int[] BuildMemoryWordIndexes(string text)
    {
        var indexes = new int[text.Length];
        var word = -1;
        var insideWord = false;
        for (var index = 0; index < text.Length; index++)
        {
            if (char.IsWhiteSpace(text[index]))
            {
                insideWord = false;
                indexes[index] = Math.Max(0, word);
            }
            else
            {
                if (!insideWord)
                {
                    word++;
                    insideWord = true;
                }
                indexes[index] = word;
            }
        }
        return indexes;
    }

    private static bool ShouldShowMemoryWord(string id, int sectionIndex, int wordIndex, int cueLevel)
    {
        var visiblePercent = cueLevel switch { 0 => 100, 1 => 70, 2 => 40, 3 => 15, _ => 0 };
        if (visiblePercent == 100)
        {
            return true;
        }
        if (visiblePercent == 0)
        {
            return false;
        }

        // Keep one dependable starting cue until the intentional no-cues stage.
        if (wordIndex == 0)
        {
            return true;
        }

        var seed = 17;
        foreach (var character in id)
        {
            seed = unchecked((seed * 31) + character);
        }
        // 37 is coprime with 100, which distributes neighboring words around the
        // cue range instead of clustering every word in a short verse together.
        var baseScore = (seed & int.MaxValue) % 100;
        var score = (baseScore + (sectionIndex * 19) + (wordIndex * 37)) % 100;
        return score < visiblePercent;
    }

    private void UpdateMemorySmoothCaret(bool animate)
    {
        if (_memoryCaretPointer is null || !MemoryPracticeView.IsVisible)
        {
            return;
        }
        var rect = _memoryCaretPointer.GetCharacterRect(LogicalDirection.Forward);
        var target = MemoryPracticeTextView.TranslatePoint(new Point(rect.X, rect.Y), MemoryPracticeCaretLayer);
        MemorySmoothCaret.Height = Math.Max(30, rect.Height);
        var currentLeft = Canvas.GetLeft(MemorySmoothCaret);
        var currentTop = Canvas.GetTop(MemorySmoothCaret);
        if (!animate || double.IsNaN(currentLeft) || double.IsNaN(currentTop))
        {
            MemorySmoothCaret.BeginAnimation(Canvas.LeftProperty, null);
            MemorySmoothCaret.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(MemorySmoothCaret, target.X);
            Canvas.SetTop(MemorySmoothCaret, target.Y);
            return;
        }
        var duration = TimeSpan.FromMilliseconds(88);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        MemorySmoothCaret.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(target.X, duration) { EasingFunction = ease });
        MemorySmoothCaret.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(target.Y, duration) { EasingFunction = ease });
    }

    private void CompleteMemorySection()
    {
        if (_activeMemoryPassage is null)
        {
            return;
        }
        var section = CurrentMemorySection;
        section.Attempts++;
        var accuracy = section.Text.Length == 0
            ? 1
            : 1 - ((double)_memoryTypedErrors.Count(value => value) / section.Text.Length);
        _activeMemoryPassage.LastPracticedAt = DateTimeOffset.Now;
        if (accuracy < 0.9)
        {
            MemoryPracticeFeedback.Text = $"{accuracy:P0} accuracy · restart this section when ready";
            QueueWorkspaceSave();
            return;
        }

        section.SuccessfulRepetitions++;
        if (section.SuccessfulRepetitions % 2 == 0 && section.CueLevel < 4)
        {
            section.CueLevel++;
        }
        QueueWorkspaceSave();
        MemoryPracticeFeedback.Text = accuracy >= 0.99 ? "Section complete · clean recall" : $"Section complete · {accuracy:P0} accuracy";

        var completedGeneration = _memorySectionGeneration;
        var timer = new DispatcherTimer(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(650) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            if (_activeMemoryPassage is null || completedGeneration != _memorySectionGeneration)
            {
                return;
            }
            if (_activeMemorySectionIndex + 1 < _activeMemoryPassage.Sections.Count)
            {
                StartMemorySection(_activeMemorySectionIndex + 1, animate: true);
            }
            else
            {
                _memorySessionComplete = true;
                MemoryPracticeFeedback.Text = "Passage complete · press Enter to practice again";
                RefreshMemoryLibrary();
            }
        };
        timer.Start();
    }

    private void MemoryRestartSection_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMemoryPassage is not null)
        {
            StartMemorySection(_activeMemorySectionIndex, animate: true);
            MemoryPracticeView.Focus();
        }
    }

    private void MemoryShowMoreWords_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMemoryPassage is null)
        {
            return;
        }
        _memoryShowMoreWords = true;
        _memoryTypedText = string.Empty;
        _memoryTypedErrors.Clear();
        RenderMemoryPracticeText(animateCaret: false);
        MemoryPracticeView.Focus();
    }

    private void MemoryRelearnSection_Click(object sender, RoutedEventArgs e)
    {
        if (_activeMemoryPassage is null)
        {
            return;
        }
        var section = CurrentMemorySection;
        section.CueLevel = Math.Max(0, section.CueLevel - 1);
        section.SuccessfulRepetitions = 0;
        QueueWorkspaceSave();
        StartMemorySection(_activeMemorySectionIndex, animate: true);
        ShowToast("More cues restored");
        MemoryPracticeView.Focus();
    }

    private void OpenBook(BibleBook book)
    {
        _selectedBook = book;
        _currentStudy = null;
        _currentContainer = GetBookWorkspace(book);

        LibraryPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        StudyPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Visible;
        RestoreContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Visible;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.BibleStudy);

        RenderWorkspace();
        RenderBreadcrumbs();
    }

    private WorkspaceItem GetBookWorkspace(BibleBook book)
    {
        if (_workspaceByBook.TryGetValue(book.Name, out var workspace))
        {
            return workspace;
        }

        workspace = new WorkspaceItem(book.Name, WorkspaceItemKind.Book, book.AccentBrush, null);
        _workspaceByBook[book.Name] = workspace;
        return workspace;
    }

    private void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        AddWorkspaceItem(WorkspaceItemKind.Folder);
    }

    private void AddStudy_Click(object sender, RoutedEventArgs e)
    {
        AddWorkspaceItem(WorkspaceItemKind.Study);
    }

    private void AddWorkspaceItem(WorkspaceItemKind kind)
    {
        if (_currentContainer is null || _selectedBook is null)
        {
            return;
        }

        var baseName = kind == WorkspaceItemKind.Folder ? "New Folder" : "New Study";
        var item = new WorkspaceItem(
            CreateUniqueName(baseName, _currentContainer.Children),
            kind,
            PickAccentBrush(_currentContainer.Children.Count),
            _currentContainer);

        _currentContainer.Children.Add(item);
        _currentContainer.RefreshDetail();
        QueueWorkspaceSave();
        RenderWorkspace();
        BeginWorkspaceItemRename(item);
        ShowToast(kind == WorkspaceItemKind.Folder
            ? "Folder created"
            : "Study created");
    }

    private void WorkspaceItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkspaceItem item })
        {
            return;
        }

        RunPageTransition(sender, () => OpenWorkspaceItem(item));
    }

    private void OpenWorkspaceItem(WorkspaceItem item)
    {
        if (item.Kind == WorkspaceItemKind.Folder)
        {
            _currentContainer = item;
            _currentStudy = null;
            TodayPanel.Visibility = Visibility.Collapsed;
            ReminderSettingsPanel.Visibility = Visibility.Collapsed;
            SetSettingsPanelVisibility(Visibility.Collapsed);
            WorkspacePanel.Visibility = Visibility.Visible;
            StudyPanel.Visibility = Visibility.Collapsed;
            RenderWorkspace();
        }
        else if (item.Kind == WorkspaceItemKind.Study)
        {
            OpenStudy(item);
        }

        RenderBreadcrumbs();
    }

    private void BeginWorkspaceItemRename(WorkspaceItem item)
    {
        if (item.Kind == WorkspaceItemKind.Book)
        {
            return;
        }

        item.BeginRename();
        Dispatcher.BeginInvoke(() =>
        {
            var textBox = FindWorkspaceRenameTextBox(item);
            if (textBox is null)
            {
                return;
            }

            textBox.Focus();
            textBox.SelectAll();
        }, DispatcherPriority.Background);
    }

    private void WorkspaceRenameTextBox_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox { Tag: WorkspaceItem { IsRenaming: true } } textBox)
        {
            Dispatcher.BeginInvoke(() =>
            {
                textBox.Focus();
                textBox.SelectAll();
            }, DispatcherPriority.Background);
        }
    }

    private void WorkspaceRenameTextBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (sender is TextBox textBox && !textBox.IsKeyboardFocusWithin)
        {
            textBox.Focus();
            textBox.SelectAll();
        }
    }

    private void WorkspaceRenameTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox { Tag: WorkspaceItem item } textBox)
        {
            return;
        }

        if (e.Key == Key.Enter)
        {
            _suppressRenameCommit = true;
            CommitWorkspaceItemRename(item, textBox.Text);
            Keyboard.ClearFocus();
            _suppressRenameCommit = false;
            e.Handled = true;
            return;
        }

        if (e.Key == Key.Escape)
        {
            _suppressRenameCommit = true;
            item.CancelRename();
            Keyboard.ClearFocus();
            _suppressRenameCommit = false;
            e.Handled = true;
        }
    }

    private void WorkspaceRenameTextBox_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (_suppressRenameCommit || sender is not TextBox { Tag: WorkspaceItem item })
        {
            return;
        }

        CommitWorkspaceItemRename(item, item.Name);
    }

    private void CommitWorkspaceItemRename(WorkspaceItem item, string requestedName)
    {
        var parent = item.Parent;
        var fallbackName = string.IsNullOrWhiteSpace(item.RenameOriginalName)
            ? (item.Kind == WorkspaceItemKind.Folder ? "New Folder" : "New Study")
            : item.RenameOriginalName;
        var trimmedName = string.IsNullOrWhiteSpace(requestedName)
            ? fallbackName
            : requestedName.Trim();

        if (parent is not null)
        {
            trimmedName = CreateUniqueName(trimmedName, parent.Children.Where(sibling => !ReferenceEquals(sibling, item)));
        }

        item.Name = trimmedName;
        item.EndRename();
        parent?.RefreshDetail();
        if (ReferenceEquals(_currentStudy, item))
        {
            MainHeading.Text = item.Name;
            MainSubheading.Visibility = Visibility.Collapsed;
        }

        RenderBreadcrumbs();
        QueueWorkspaceSave();
        ShowToast("Name saved");
    }

    private void BackOneLevel_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, BackOneLevel);
    }

    private void BackOneLevel()
    {
        if (IsColorThemeSettingsPanelVisible() || IsLocalAiSettingsPanelVisible() || ReminderSettingsPanel.Visibility == Visibility.Visible)
        {
            ShowSettings();
            return;
        }

        if (_currentStudy is not null)
        {
            _currentStudy = null;
            TodayPanel.Visibility = Visibility.Collapsed;
            ReminderSettingsPanel.Visibility = Visibility.Collapsed;
            SetSettingsPanelVisibility(Visibility.Collapsed);
            WorkspacePanel.Visibility = Visibility.Visible;
            StudyPanel.Visibility = Visibility.Collapsed;
            RestoreContentShell();
            HeaderBackButton.Visibility = Visibility.Collapsed;
            WorkspaceStatusCard.Visibility = Visibility.Visible;
            PassagePickerCard.Visibility = Visibility.Collapsed;
            RenderWorkspace();
            RenderBreadcrumbs();
            return;
        }

        if (_currentContainer?.Parent is not null)
        {
            _currentContainer = _currentContainer.Parent;
            RenderWorkspace();
            RenderBreadcrumbs();
            return;
        }

        BackToLibrary();
    }

    private void BackToLibrary()
    {
        _selectedBook = null;
        _currentContainer = null;
        _currentStudy = null;

        LibraryPanel.Visibility = Visibility.Visible;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        MainHeading.Text = "Bible Study";
        MainSubheading.Visibility = Visibility.Collapsed;
        RestoreContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Visible;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.BibleStudy);
        RenderBreadcrumbs();
    }

    private void ShowTodayDashboard()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Visible;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        MainHeading.Text = "Today";
        MainSubheading.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy");
        MainSubheading.Visibility = Visibility.Visible;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Today);
        RenderTodayDashboard();
        RenderBreadcrumbs();
    }

    private void ReminderSettings_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowReminderSettings);
    }

    private void ShowReminderSettings()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Visible;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        MainHeading.Text = "Reminder Settings";
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Visible;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Settings);
        RenderBreadcrumbs();
    }

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowSettings);
    }

    private void ColorThemeSettings_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowColorThemeSettings);
    }

    private void LocalAiSettings_Click(object sender, RoutedEventArgs e)
    {
        RunPageTransition(sender, ShowLocalAiSettings);
    }

    private void ShowSettings()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Visible);
        SetColorThemeSettingsPanelVisibility(Visibility.Collapsed);
        MainHeading.Text = "Settings";
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Settings);
        RenderBreadcrumbs();
    }

    private void ShowLocalAiSettings()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        SetLocalAiSettingsPanelVisibility(Visibility.Visible);
        MainHeading.Text = "Local AI Model";
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Visible;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Settings);
        RenderBreadcrumbs();
        _ = RefreshAiModelsAsync();
    }

    private async void RefreshAiModels_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAiModelsAsync();
    }

    private async Task RefreshAiModelsAsync()
    {
        if (_isRefreshingAiModels || AiModelComboBox is null || AiModelStatusText is null)
        {
            return;
        }

        _isRefreshingAiModels = true;
        RefreshAiModelsButton.IsEnabled = false;
        AiModelComboBox.IsEnabled = false;
        AiModelStatusText.Text = "Starting Ollama and checking downloaded models...";
        try
        {
            await EnsureBackendRunningAsync();
            using var response = await BackendHttpClient.GetAsync($"{BackendApiUrl}/api/ai/status");
            if (!response.IsSuccessStatusCode)
            {
                var error = await response.Content.ReadAsStringAsync();
                throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {error}");
            }

            var status = await response.Content.ReadFromJsonAsync<OllamaStatusApiResponse>()
                ?? throw new InvalidOperationException("Backend returned an empty Ollama status response.");
            _availableAiModels.Clear();
            foreach (var model in status.Models
                         .Where(model => !string.IsNullOrWhiteSpace(model))
                         .Distinct(StringComparer.OrdinalIgnoreCase)
                         .OrderBy(model => model, StringComparer.OrdinalIgnoreCase))
            {
                _availableAiModels.Add(model);
            }

            if (status.Running && _availableAiModels.Count > 0)
            {
                var selectedModel = _availableAiModels.FirstOrDefault(model =>
                                        string.Equals(model, _selectedAiModel, StringComparison.OrdinalIgnoreCase))
                                    ?? _availableAiModels.FirstOrDefault(model =>
                                        string.Equals(model, status.ConfiguredModel, StringComparison.OrdinalIgnoreCase))
                                    ?? _availableAiModels[0];
                _selectedAiModel = selectedModel;
                AiModelComboBox.SelectedItem = selectedModel;
                AiModelComboBox.IsEnabled = true;
                AiModelStatusText.Text = $"Ollama is running. {_availableAiModels.Count} downloaded model{(_availableAiModels.Count == 1 ? string.Empty : "s")} available.";
                QueueWorkspaceSave();
            }
            else
            {
                AiModelComboBox.SelectedIndex = -1;
                AiModelStatusText.Text = status.Error
                    ?? "Ollama is not running or has no downloaded models.";
            }
        }
        catch (Exception ex)
        {
            _availableAiModels.Clear();
            AiModelComboBox.SelectedIndex = -1;
            AiModelStatusText.Text = $"Could not check Ollama: {FormatExceptionMessage(ex)}";
        }
        finally
        {
            RefreshAiModelsButton.IsEnabled = true;
            _isRefreshingAiModels = false;
        }
    }

    private void AiModelComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isRefreshingAiModels || AiModelComboBox.SelectedItem is not string model)
        {
            return;
        }

        _selectedAiModel = model;
        QueueWorkspaceSave();
        ShowToast($"AI model set to {model}");
    }

    private void ShowColorThemeSettings()
    {
        _currentStudy = null;
        LibraryPanel.Visibility = Visibility.Collapsed;
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Collapsed;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        SetColorThemeSettingsPanelVisibility(Visibility.Visible);
        MainHeading.Text = "Color Theme";
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Visible;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Collapsed;
        SetActiveNavTab(AppNavTab.Settings);
        RenderBreadcrumbs();
    }

    private void RenderWorkspace()
    {
        if (_currentContainer is null || _selectedBook is null)
        {
            return;
        }

        WorkspaceTitle.Text = _currentContainer.Name;
        WorkspaceSubtitle.Text = _currentContainer.Kind == WorkspaceItemKind.Book
            ? $"{_selectedBook.ChapterLabel} in the {_selectedBook.Testament}"
            : "Folders and studies in this folder";
        WorkspaceItems.ItemsSource = _currentContainer.Children;
        EmptyWorkspaceText.Visibility = _currentContainer.Children.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        MainHeading.Text = _selectedBook.Name;
        MainSubheading.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        RestoreContentShell();
        HeaderBackButton.Visibility = Visibility.Collapsed;
        WorkspaceStatusCard.Visibility = Visibility.Visible;
        PassagePickerCard.Visibility = Visibility.Collapsed;
    }

    private void RenderBreadcrumbs()
    {
        BreadcrumbTrail.Children.Clear();
        AddBreadcrumb("Bible Study", BreadcrumbTarget.Library, null, true);

        if (TodayPanel.Visibility == Visibility.Visible)
        {
            AddBreadcrumb("Today", BreadcrumbTarget.Today, null, false);
            return;
        }

        if (ReminderSettingsPanel.Visibility == Visibility.Visible)
        {
            AddBreadcrumb("Settings", BreadcrumbTarget.Settings, null, false);
            AddBreadcrumb("Reminder Settings", BreadcrumbTarget.ReminderSettings, null, false);
            return;
        }

        if (IsColorThemeSettingsPanelVisible())
        {
            AddBreadcrumb("Settings", BreadcrumbTarget.Settings, null, false);
            AddBreadcrumb("Color Theme", BreadcrumbTarget.ColorThemeSettings, null, false);
            return;
        }

        if (IsLocalAiSettingsPanelVisible())
        {
            AddBreadcrumb("Settings", BreadcrumbTarget.Settings, null, false);
            AddBreadcrumb("Local AI Model", BreadcrumbTarget.LocalAiSettings, null, false);
            return;
        }

        if (IsSettingsPanelVisible())
        {
            AddBreadcrumb("Settings", BreadcrumbTarget.Settings, null, false);
            return;
        }

        if (_selectedBook is null || _currentContainer is null)
        {
            return;
        }

        var lineage = GetContainerLineage(_currentContainer);
        foreach (var node in lineage)
        {
            AddBreadcrumb(node.Name, BreadcrumbTarget.WorkspaceItem, node, false);
        }

        if (_currentStudy is not null)
        {
            AddBreadcrumb(_currentStudy.Name, BreadcrumbTarget.Study, _currentStudy, false);
        }
    }

    private void SetSettingsPanelVisibility(Visibility visibility)
    {
        if (SettingsPanel is not null)
        {
            SettingsPanel.Visibility = visibility;
        }

        SetColorThemeSettingsPanelVisibility(Visibility.Collapsed);
        SetLocalAiSettingsPanelVisibility(Visibility.Collapsed);
    }

    private bool IsSettingsPanelVisible()
    {
        return SettingsPanel is not null && SettingsPanel.Visibility == Visibility.Visible;
    }

    private void SetColorThemeSettingsPanelVisibility(Visibility visibility)
    {
        if (ColorThemeSettingsPanel is not null)
        {
            ColorThemeSettingsPanel.Visibility = visibility;
        }
    }

    private bool IsColorThemeSettingsPanelVisible()
    {
        return ColorThemeSettingsPanel is not null && ColorThemeSettingsPanel.Visibility == Visibility.Visible;
    }

    private void SetLocalAiSettingsPanelVisibility(Visibility visibility)
    {
        if (LocalAiSettingsPanel is not null)
        {
            LocalAiSettingsPanel.Visibility = visibility;
        }
    }

    private bool IsLocalAiSettingsPanelVisible()
    {
        return LocalAiSettingsPanel is not null && LocalAiSettingsPanel.Visibility == Visibility.Visible;
    }

    private void AddBreadcrumb(string text, BreadcrumbTarget target, object? tag, bool isFirst)
    {
        if (BreadcrumbTrail is null)
        {
            return;
        }

        if (!isFirst)
        {
            BreadcrumbTrail.Children.Add(new TextBlock
            {
                Text = " / ",
                Foreground = GetResourceBrush("TextMuted"),
                FontWeight = FontWeights.SemiBold
            });
        }

        var button = new Button();
        button.Content = text;
        button.Tag = new BreadcrumbPayload(target, tag);
        button.Background = Brushes.Transparent;
        button.BorderThickness = new Thickness(0);
        button.Padding = new Thickness(0);
        button.Cursor = Cursors.Hand;
        button.FontWeight = FontWeights.SemiBold;
        button.Foreground = target == BreadcrumbTarget.Library
            ? GetResourceBrush("Mint")
            : GetResourceBrush("TextSecondary");
        button.Click += Breadcrumb_Click;
        BreadcrumbTrail.Children.Add(button);
    }

    private void Breadcrumb_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: BreadcrumbPayload payload })
        {
            return;
        }

        RunPageTransition(sender, () => NavigateBreadcrumb(payload));
    }

    private void NavigateBreadcrumb(BreadcrumbPayload payload)
    {
        switch (payload.Target)
        {
            case BreadcrumbTarget.Library:
                BackToLibrary();
                break;
            case BreadcrumbTarget.WorkspaceItem when payload.Value is WorkspaceItem item:
                _currentContainer = item;
                _currentStudy = null;
                LibraryPanel.Visibility = Visibility.Collapsed;
                TodayPanel.Visibility = Visibility.Collapsed;
                ReminderSettingsPanel.Visibility = Visibility.Collapsed;
                SetSettingsPanelVisibility(Visibility.Collapsed);
                WorkspacePanel.Visibility = Visibility.Visible;
                StudyPanel.Visibility = Visibility.Collapsed;
                RestoreContentShell();
                HeaderBackButton.Visibility = Visibility.Collapsed;
                WorkspaceStatusCard.Visibility = Visibility.Visible;
                PassagePickerCard.Visibility = Visibility.Collapsed;
                RenderWorkspace();
                RenderBreadcrumbs();
                break;
            case BreadcrumbTarget.Study when payload.Value is WorkspaceItem study:
                OpenStudy(study);
                RenderBreadcrumbs();
                break;
            case BreadcrumbTarget.Today:
                ShowTodayDashboard();
                break;
            case BreadcrumbTarget.ReminderSettings:
                ShowReminderSettings();
                break;
            case BreadcrumbTarget.Settings:
                ShowSettings();
                break;
            case BreadcrumbTarget.ColorThemeSettings:
                ShowColorThemeSettings();
                break;
            case BreadcrumbTarget.LocalAiSettings:
                ShowLocalAiSettings();
                break;
        }
    }

    private void OpenStudy(WorkspaceItem study)
    {
        _isRestoringStudyPanelGeometry = true;
        _currentStudy = study;
        TodayPanel.Visibility = Visibility.Collapsed;
        ReminderSettingsPanel.Visibility = Visibility.Collapsed;
        SetSettingsPanelVisibility(Visibility.Collapsed);
        WorkspacePanel.Visibility = Visibility.Collapsed;
        StudyPanel.Visibility = Visibility.Visible;
        MainHeading.Text = study.Name;
        MainSubheading.Visibility = Visibility.Collapsed;
        FlattenContentShell();
        HeaderBackButton.Visibility = Visibility.Visible;
        WorkspaceStatusCard.Visibility = Visibility.Collapsed;
        PassagePickerCard.Visibility = Visibility.Visible;
        EnsureStudyHasBlocks(study);
        UpdateNumberedListLabels();
        ConfigurePassageSelectors(study);
        StudyBlockItems.ItemsSource = study.Blocks;
        HideSlashCommandMenu();
        SetActiveNavTab(AppNavTab.BibleStudy);
        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                ApplyStudyPanelGeometry(study);
                RenderExtraStudyPanels();
                RefreshVisibleAnnotationRendering();
            }
            finally
            {
                _isRestoringStudyPanelGeometry = false;
            }
        }, DispatcherPriority.Loaded);
        FocusBlock(study.Blocks[0]);
    }

    private void SetActiveNavTab(AppNavTab activeTab)
    {
        CloseSelectionUi();
        if (activeTab != AppNavTab.ScriptureMemory)
        {
            ScriptureMemoryPanel.Visibility = Visibility.Collapsed;
        }
        _activeNavTab = activeTab;
        BibleStudyNavButton.Style = (Style)FindResource(activeTab == AppNavTab.BibleStudy
            ? "ActiveNavButtonStyle"
            : "NavButtonStyle");
        TodayNavButton.Style = (Style)FindResource(activeTab == AppNavTab.Today
            ? "ActiveNavButtonStyle"
            : "NavButtonStyle");
        ScriptureMemoryNavButton.Style = (Style)FindResource(activeTab == AppNavTab.ScriptureMemory
            ? "ActiveNavButtonStyle"
            : "NavButtonStyle");
        SettingsNavButton.Style = (Style)FindResource(activeTab == AppNavTab.Settings
            ? "ActiveNavButtonStyle"
            : "NavButtonStyle");
        AnimateNavTabIndicator(GetActiveNavButton(activeTab));
    }

    private void SidebarRoot_Loaded(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() => PositionNavTabIndicator(GetActiveNavButton(_activeNavTab)), DispatcherPriority.ContextIdle);
    }

    private void SidebarNavButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Button button && button.IsEnabled && button.Visibility == Visibility.Visible)
        {
            AnimateNavTabIndicator(button, TimeSpan.FromMilliseconds(150));
        }
    }

    private Button GetActiveNavButton(AppNavTab activeTab)
    {
        return activeTab switch
        {
            AppNavTab.Today => TodayNavButton,
            AppNavTab.ScriptureMemory => ScriptureMemoryNavButton,
            AppNavTab.Settings => SettingsNavButton,
            _ => BibleStudyNavButton
        };
    }

    private void PositionNavTabIndicator(Button targetButton)
    {
        if (NavTabIndicator is null || NavTabIndicatorLayer is null || targetButton is null || !targetButton.IsVisible)
        {
            return;
        }

        var targetPoint = targetButton.TranslatePoint(new Point(0, 0), NavTabIndicatorLayer);
        Canvas.SetLeft(NavTabIndicator, targetPoint.X);
        Canvas.SetTop(NavTabIndicator, targetPoint.Y);
        NavTabIndicator.Width = targetButton.ActualWidth;
        NavTabIndicator.Height = targetButton.ActualHeight;
    }

    private void AnimateNavTabIndicator(Button targetButton)
    {
        AnimateNavTabIndicator(targetButton, TimeSpan.FromMilliseconds(220));
    }

    private void AnimateNavTabIndicator(Button targetButton, TimeSpan duration)
    {
        if (NavTabIndicator is null || NavTabIndicatorLayer is null || targetButton is null || !targetButton.IsVisible)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            try
            {
                var targetPoint = targetButton.TranslatePoint(new Point(0, 0), NavTabIndicatorLayer);
                var currentTop = Canvas.GetTop(NavTabIndicator);
                var targetTop = targetPoint.Y;
                var targetLeft = targetPoint.X;
                var targetWidth = targetButton.ActualWidth;
                var targetHeight = targetButton.ActualHeight;
                if (double.IsNaN(currentTop))
                {
                    PositionNavTabIndicator(targetButton);
                    return;
                }

                var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };
                NavTabIndicator.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(targetLeft, duration)
                {
                    EasingFunction = ease
                });
                NavTabIndicator.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(targetTop, duration)
                {
                    EasingFunction = ease
                });
                NavTabIndicator.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(targetWidth, duration)
                {
                    EasingFunction = ease
                });
                NavTabIndicator.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(targetHeight, TimeSpan.FromMilliseconds(Math.Min(180, duration.TotalMilliseconds)))
                {
                    EasingFunction = ease
                });
            }
            catch
            {
            }
        }, DispatcherPriority.Loaded);
    }

    private void TodayStudy_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: WorkspaceItem study })
        {
            return;
        }

        RunPageTransition(sender, () => OpenTodayStudy(study));
    }

    private void OpenTodayStudy(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        _selectedBook = BibleBooks.FirstOrDefault(book => book.Name == root.Name);
        _currentContainer = study.Parent ?? root;
        OpenStudy(study);
        RenderBreadcrumbs();
    }

    private void RenderTodayDashboard()
    {
        MainSubheading.Text = DateTime.Now.ToString("dddd, MMMM d, yyyy");
        _todayEditedStudies.Clear();

        var today = DateTime.Today;
        var dayState = GetTodayState();
        foreach (var study in GetAllStudies()
                     .Where(study => study.LastEditedAt?.Date == today)
                     .Where(study => !dayState.ExcludedStudyContextKeys.Contains(CreateStudyContextKey(study), StringComparer.OrdinalIgnoreCase))
                     .OrderByDescending(study => study.LastEditedAt))
        {
            _todayEditedStudies.Add(CreateDailyStudyEntry(study));
        }

        TodayEditedCountText.Text = _todayEditedStudies.Count == 1
            ? "1 study edited"
            : $"{_todayEditedStudies.Count} studies edited";
        EmptyTodayText.Visibility = _todayEditedStudies.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        _isLoadingDailyDetails = true;
        DailyDetailsTextBox.Text = dayState.Details;
        _isLoadingDailyDetails = false;
        UpdateDailyDetailsPlaceholder();
        RenderScheduledNotifications();
    }

    private void RemoveTodayEditedStudyContext_Click(object sender, RoutedEventArgs e)
    {
        e.Handled = true;

        if (sender is not Button { Tag: string contextKey } || string.IsNullOrWhiteSpace(contextKey))
        {
            return;
        }

        var dayState = GetTodayState();
        if (!dayState.ExcludedStudyContextKeys.Contains(contextKey, StringComparer.OrdinalIgnoreCase))
        {
            dayState.ExcludedStudyContextKeys.Add(contextKey);
        }

        SaveWorkspaceState();
        RenderTodayDashboard();
        ShowToast("Deleted from today's AI context");
    }

    private void RenderScheduledNotifications()
    {
        _scheduledNotificationDisplays.Clear();

        foreach (var notification in _scheduledNotifications
                     .OrderByDescending(notification => notification.CreatedAt)
                     .Take(12))
        {
            _scheduledNotificationDisplays.Add(new ScheduledNotificationDisplay(notification));
        }

        var activeCount = _scheduledNotifications.Count(notification => !notification.Deleted);
        var deletedCount = _scheduledNotifications.Count(notification => notification.Deleted);

        ScheduledNotificationSummaryText.Text = activeCount == 0
            ? "No active scheduled messages."
            : activeCount == 1
                ? "1 active scheduled message can be canceled."
                : $"{activeCount} active scheduled messages can be canceled.";
        if (deletedCount > 0)
        {
            ScheduledNotificationSummaryText.Text += $" {deletedCount} deleted.";
        }

        EmptyScheduledNotificationText.Visibility = _scheduledNotificationDisplays.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        UpdateCancelScheduledNotificationsButton();
    }

    private void RemoveDeletedScheduledNotification_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string sequenceId })
        {
            return;
        }

        var removedCount = _scheduledNotifications.RemoveAll(notification =>
            notification.Deleted
            && string.Equals(notification.SequenceId, sequenceId, StringComparison.OrdinalIgnoreCase));
        if (removedCount == 0)
        {
            return;
        }

        SaveWorkspaceState();
        RenderScheduledNotifications();
        ShowToast("Deleted message removed");
    }

    private DailyStudyEntry CreateDailyStudyEntry(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        var lineage = GetContainerLineage(study);
        var folders = lineage
            .Skip(1)
            .Take(Math.Max(0, lineage.Count - 2))
            .Select(item => item.Name);
        var folderPath = string.Join(" / ", folders);
        var location = string.IsNullOrWhiteSpace(folderPath)
            ? root.Name
            : $"{root.Name} / {folderPath}";

        return new DailyStudyEntry(
            study,
            study.Name,
            location,
            study.PassageLabel,
            study.LastEditedAt?.ToString("h:mm tt") ?? string.Empty,
            CreateStudyContextKey(study));
    }

    private string CreateStudyContextKey(WorkspaceItem study)
    {
        return string.Join("/", GetContainerLineage(study).Select(item => item.Name));
    }

    private static string TrimForReminder(string text)
    {
        var compact = string.Join(" ", text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
        return compact.Length <= 120
            ? compact
            : $"{compact[..117]}...";
    }

    private static string FillReminderPrompt(string prompt, string studyName, string passage, string details)
    {
        var detailText = string.IsNullOrWhiteSpace(details)
            ? string.Empty
            : TrimForReminder(details);

        var template = string.IsNullOrWhiteSpace(prompt)
            ? "Return to {passage}."
            : prompt;

        return template
            .Replace("{study}", studyName, StringComparison.OrdinalIgnoreCase)
            .Replace("{passage}", passage, StringComparison.OrdinalIgnoreCase)
            .Replace("{details}", detailText, StringComparison.OrdinalIgnoreCase)
            .Trim();
    }

    private static string NormalizeReminderInstruction(string prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? "Write a short reminder that brings me back to this study."
            : prompt.Trim();
    }

    private static string NormalizeOverviewInstruction(string prompt)
    {
        return string.IsNullOrWhiteSpace(prompt)
            ? DefaultOverviewPrompt
            : prompt.Trim();
    }

    private void ReminderSettings_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitializing || _isLoadingReminderSettings)
        {
            return;
        }

        QueueWorkspaceSave();
        if (sender == OverviewNotificationEnabledCheckBox)
        {
            ShowToast(OverviewNotificationEnabledCheckBox.IsChecked == true
                ? "Overview enabled"
                : "Overview disabled");
            return;
        }

        ShowToast(RemindersEnabledCheckBox.IsChecked == true
            ? "Follow-ups enabled"
            : "Follow-ups disabled");
    }

    private void ReminderSettingsTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInitializing || _isLoadingReminderSettings)
        {
            return;
        }

        QueueWorkspaceSave();
    }

    private void AddReminder_Click(object sender, RoutedEventArgs e)
    {
        AddReminderScheduleItem(new ReminderScheduleItem(
            1,
            "hours",
            "Write a short reminder that brings me back to this study."),
            insertFirst: true);
        QueueWorkspaceSave();
        ShowToast("Reminder added");
    }

    private void RemoveReminder_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ReminderScheduleItem item })
        {
            return;
        }

        item.PropertyChanged -= ReminderScheduleItem_PropertyChanged;
        _reminderScheduleItems.Remove(item);
        UpdateReminderCount();
        QueueWorkspaceSave();
        ShowToast("Reminder removed");
    }

    private void ReminderPresetSelect_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingReminderSettings || ReminderPresetSelect.SelectedItem is not ReminderPreset preset)
        {
            return;
        }

        NewReminderPresetNameTextBox.Text = preset.Name;
        ApplyReminderPreset(preset);
        QueueWorkspaceSave();
        ShowToast("Preset activated");
    }

    private void ApplyReminderPreset_Click(object sender, RoutedEventArgs e)
    {
        if (ReminderPresetSelect.SelectedItem is not ReminderPreset preset)
        {
            return;
        }

        ApplyReminderPreset(preset);
        QueueWorkspaceSave();
        ShowToast("Preset activated");
    }

    private void ApplyReminderPreset(ReminderPreset preset)
    {
        foreach (var item in _reminderScheduleItems)
        {
            item.PropertyChanged -= ReminderScheduleItem_PropertyChanged;
        }

        _reminderScheduleItems.Clear();
        foreach (var item in preset.Reminders)
        {
            AddReminderScheduleItem(item.Clone());
        }

        UpdateReminderCount();
    }

    private void UpdateReminderCount()
    {
        if (ReminderCountText is null)
        {
            return;
        }

        ReminderCountText.Text = _reminderScheduleItems.Count == 1
            ? "1 follow-up reminder"
            : $"{_reminderScheduleItems.Count} follow-up reminders";
    }

    private void SaveReminderPreset_Click(object sender, RoutedEventArgs e)
    {
        var presetName = string.IsNullOrWhiteSpace(NewReminderPresetNameTextBox.Text)
            ? $"Default {_reminderPresets.Count + 1}"
            : NewReminderPresetNameTextBox.Text.Trim();
        var reminders = _reminderScheduleItems
            .Select(item => item.Clone())
            .ToList();
        var existing = _reminderPresets.FirstOrDefault(preset => preset.Name.Equals(presetName, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            var index = _reminderPresets.IndexOf(existing);
            _reminderPresets[index] = new ReminderPreset(presetName, reminders);
            _isLoadingReminderSettings = true;
            ReminderPresetSelect.SelectedIndex = index;
            _isLoadingReminderSettings = false;
        }
        else
        {
            var preset = new ReminderPreset(presetName, reminders);
            _reminderPresets.Add(preset);
            _isLoadingReminderSettings = true;
            ReminderPresetSelect.SelectedItem = preset;
            _isLoadingReminderSettings = false;
        }

        QueueWorkspaceSave();
        ShowToast("Default saved");
    }

    private void DailyDetails_TextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateDailyDetailsPlaceholder();
        if (_isLoadingDailyDetails)
        {
            return;
        }

        var todayState = GetTodayState();
        todayState.Details = DailyDetailsTextBox.Text;
        QueueWorkspaceSave();
    }

    private void UpdateDailyDetailsPlaceholder()
    {
        if (DailyDetailsPlaceholderText is null || DailyDetailsTextBox is null)
        {
            return;
        }

        DailyDetailsPlaceholderText.Visibility = string.IsNullOrWhiteSpace(DailyDetailsTextBox.Text)
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void ApplyColorScheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ColorSchemePreset preset })
        {
            return;
        }

        ApplyColorScheme(preset);
        SaveWorkspaceState();
        ShowToast("Color scheme applied");
    }

    private void PickColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: TextBox textBox })
        {
            return;
        }

        var currentColor = TryParseColor(textBox.Text, out var parsedColor)
            ? parsedColor
            : Colors.White;

        if (!ShowColorPickerDialog(currentColor, out var selectedColor))
        {
            return;
        }

        textBox.Text = ToHex(selectedColor);
    }

    private bool ShowColorPickerDialog(Color initialColor, out Color selectedColor)
    {
        selectedColor = initialColor;

        var preview = new Border
        {
            Height = 48,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush(initialColor),
            Margin = new Thickness(0, 0, 0, 14)
        };

        var redSlider = CreateColorSlider(initialColor.R);
        var greenSlider = CreateColorSlider(initialColor.G);
        var blueSlider = CreateColorSlider(initialColor.B);
        var valueText = new TextBlock
        {
            Text = ToHex(initialColor),
            Foreground = GetResourceBrush("TextSecondary"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 12)
        };

        void UpdatePreview(object? _, RoutedPropertyChangedEventArgs<double> __)
        {
            var color = Color.FromRgb((byte)redSlider.Value, (byte)greenSlider.Value, (byte)blueSlider.Value);
            preview.Background = new SolidColorBrush(color);
            valueText.Text = ToHex(color);
        }

        redSlider.ValueChanged += UpdatePreview;
        greenSlider.ValueChanged += UpdatePreview;
        blueSlider.ValueChanged += UpdatePreview;

        var okButton = new Button
        {
            Content = "Use color",
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 96,
            Margin = new Thickness(0, 0, 8, 0),
            Style = TryFindResource("AccentButtonStyle") as Style
        };
        var cancelButton = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(14, 8, 14, 8),
            MinWidth = 82,
            Style = TryFindResource("ChromeButtonStyle") as Style
        };

        var dialog = new Window
        {
            Owner = this,
            Title = "Pick color",
            Width = 360,
            Height = 340,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            ResizeMode = ResizeMode.NoResize,
            Background = GetResourceBrush("AppBackground"),
            Content = new StackPanel
            {
                Margin = new Thickness(18),
                Children =
                {
                    preview,
                    valueText,
                    CreateColorSliderRow("Red", redSlider),
                    CreateColorSliderRow("Green", greenSlider),
                    CreateColorSliderRow("Blue", blueSlider),
                    new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 18, 0, 0),
                        Children = { okButton, cancelButton }
                    }
                }
            }
        };

        okButton.Click += (_, _) => dialog.DialogResult = true;
        cancelButton.Click += (_, _) => dialog.DialogResult = false;

        if (dialog.ShowDialog() != true)
        {
            return false;
        }

        selectedColor = Color.FromRgb((byte)redSlider.Value, (byte)greenSlider.Value, (byte)blueSlider.Value);
        return true;
    }

    private static Slider CreateColorSlider(byte value)
    {
        return new Slider
        {
            Minimum = 0,
            Maximum = 255,
            Value = value,
            TickFrequency = 1,
            IsSnapToTickEnabled = true,
            Width = 220
        };
    }

    private StackPanel CreateColorSliderRow(string label, Slider slider)
    {
        return new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 8),
            Children =
            {
                new TextBlock
                {
                    Text = label,
                    Width = 58,
                    Foreground = GetResourceBrush("TextPrimary"),
                    FontWeight = FontWeights.SemiBold,
                    VerticalAlignment = VerticalAlignment.Center
                },
                slider
            }
        };
    }

    private static string ToHex(Color color)
    {
        return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }

    private void ColorHex_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox textBox || !TryParseColor(textBox.Text, out var color))
        {
            return;
        }

        var brush = new SolidColorBrush(color);
        if (ReferenceEquals(textBox, NewAppBackgroundTextBox) && NewAppBackgroundSwatch is not null)
        {
            NewAppBackgroundSwatch.Background = brush;
        }
        else if (ReferenceEquals(textBox, NewPanelBackgroundTextBox) && NewPanelBackgroundSwatch is not null)
        {
            NewPanelBackgroundSwatch.Background = brush;
        }
        else if (ReferenceEquals(textBox, NewAccentTextBox) && NewAccentSwatch is not null)
        {
            NewAccentSwatch.Background = brush;
        }
        else if (ReferenceEquals(textBox, NewTextPrimaryTextBox) && NewTextPrimarySwatch is not null)
        {
            NewTextPrimarySwatch.Background = brush;
        }
    }

    private void AddColorScheme_Click(object sender, RoutedEventArgs e)
    {
        var name = string.IsNullOrWhiteSpace(NewColorSchemeNameTextBox.Text)
            ? $"Custom {_colorSchemePresets.Count + 1}"
            : NewColorSchemeNameTextBox.Text.Trim();

        var preset = new ColorSchemePreset(
            name,
            NewAppBackgroundTextBox.Text.Trim(),
            NewAppBackgroundTextBox.Text.Trim(),
            NewPanelBackgroundTextBox.Text.Trim(),
            NewAccentTextBox.Text.Trim(),
            NewTextPrimaryTextBox.Text.Trim(),
            NewTextPrimaryTextBox.Text.Trim(),
            false);

        if (!ValidateColorScheme(preset))
        {
            ShowToast("Use valid hex colors");
            return;
        }

        var existing = _colorSchemePresets.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, preset.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null && !existing.BuiltIn)
        {
            _colorSchemePresets.Remove(existing);
        }

        _colorSchemePresets.Add(preset);
        ApplyColorScheme(preset);
        SaveWorkspaceState();
        ShowToast("Color scheme added");
    }

    private void DeleteColorScheme_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ColorSchemePreset preset })
        {
            return;
        }

        if (preset.BuiltIn)
        {
            ShowToast("Built-in schemes stay available");
            return;
        }

        _colorSchemePresets.Remove(preset);
        if (string.Equals(_selectedColorSchemeName, preset.Name, StringComparison.OrdinalIgnoreCase)
            && _colorSchemePresets.FirstOrDefault() is ColorSchemePreset fallback)
        {
            ApplyColorScheme(fallback);
        }

        SaveWorkspaceState();
        ShowToast("Color scheme deleted");
    }

    private static bool ValidateColorScheme(ColorSchemePreset preset)
    {
        return TryParseColor(preset.AppBackground, out _)
               && TryParseColor(preset.PanelBackground, out _)
               && TryParseColor(preset.Accent, out _)
               && TryParseColor(preset.TextPrimary, out _);
    }

    private DailyNoteState GetTodayState()
    {
        var dateKey = DateTime.Today.ToString("yyyy-MM-dd");
        if (_dailyNotesByDate.TryGetValue(dateKey, out var state))
        {
            return state;
        }

        state = new DailyNoteState { DateKey = dateKey };
        _dailyNotesByDate[dateKey] = state;
        return state;
    }

    private IEnumerable<WorkspaceItem> GetAllStudies()
    {
        foreach (var root in _workspaceByBook.Values)
        {
            foreach (var study in GetStudyDescendants(root))
            {
                yield return study;
            }
        }
    }

    private static IEnumerable<WorkspaceItem> GetStudyDescendants(WorkspaceItem item)
    {
        if (item.Kind == WorkspaceItemKind.Study)
        {
            yield return item;
        }

        foreach (var child in item.Children)
        {
            foreach (var descendant in GetStudyDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static WorkspaceItem GetRootWorkspace(WorkspaceItem item)
    {
        var cursor = item;
        while (cursor.Parent is not null)
        {
            cursor = cursor.Parent;
        }

        return cursor;
    }

    private void ConfigurePassageSelectors(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var chapters = Enumerable.Range(1, Math.Max(1, book?.Chapters ?? 1)).ToList();

        _isLoadingPassageSelection = true;
        StartChapterSelect.ItemsSource = chapters;
        EndChapterSelect.ItemsSource = chapters;
        StartChapterSelect.SelectedItem = study.PassageStartChapter;
        EndChapterSelect.SelectedItem = study.PassageEndChapter;
        RefreshVerseSelectors(book, study.PassageStartChapter, study.PassageEndChapter);
        StartVerseSelect.SelectedItem = ClampVerseSelection(book, study.PassageStartChapter, study.PassageStartVerse);
        EndVerseSelect.SelectedItem = ClampVerseSelection(book, study.PassageEndChapter, study.PassageEndVerse);
        PassagePreviewText.Text = study.PassageLabel;
        _isLoadingPassageSelection = false;
        RenderScripturePanel(study);
    }

    private void PassageSelection_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingPassageSelection || _currentStudy is null)
        {
            return;
        }

        var root = GetRootWorkspace(_currentStudy);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var startChapter = GetSelectedInt(StartChapterSelect);
        var endChapter = GetSelectedInt(EndChapterSelect);
        var startVerse = GetSelectedInt(StartVerseSelect);
        var endVerse = GetSelectedInt(EndVerseSelect);

        _isLoadingPassageSelection = true;
        RefreshVerseSelectors(book, startChapter, endChapter);
        StartVerseSelect.SelectedItem = ClampVerseSelection(book, startChapter, startVerse);
        EndVerseSelect.SelectedItem = ClampVerseSelection(book, endChapter, endVerse);
        _isLoadingPassageSelection = false;

        _currentStudy.PassageStartChapter = startChapter;
        _currentStudy.PassageStartVerse = GetSelectedInt(StartVerseSelect);
        _currentStudy.PassageEndChapter = endChapter;
        _currentStudy.PassageEndVerse = GetSelectedInt(EndVerseSelect);
        PassagePreviewText.Text = _currentStudy.PassageLabel;
        RenderScripturePanel(_currentStudy);
        MarkCurrentStudyEdited();
    }

    private void LoadScriptureText()
    {
        foreach (var dataSource in GetScriptureDataSources())
        {
            if (!File.Exists(dataSource.Path))
            {
                continue;
            }

            try
            {
                var json = File.ReadAllText(dataSource.Path);
                var data = JsonSerializer.Deserialize<BibleTranslationFile>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (data?.Verses is null)
                {
                    continue;
                }

                _scriptureTextByBookChapter.Clear();
                foreach (var verse in data.Verses)
                {
                    if (string.IsNullOrWhiteSpace(verse.BookName) || string.IsNullOrWhiteSpace(verse.Text))
                    {
                        continue;
                    }

                    if (!_scriptureTextByBookChapter.TryGetValue(verse.BookName, out var chapters))
                    {
                        chapters = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                        _scriptureTextByBookChapter[verse.BookName] = chapters;
                    }

                    var chapterKey = verse.Chapter.ToString();
                    if (!chapters.TryGetValue(chapterKey, out var verses))
                    {
                        verses = new List<string>();
                        chapters[chapterKey] = verses;
                    }

                    while (verses.Count < verse.Verse)
                    {
                        verses.Add(string.Empty);
                    }

                    verses[verse.Verse - 1] = verse.Text;
                }

                _scriptureTranslationLabel = dataSource.Label;
                return;
            }
            catch
            {
                _scriptureTextByBookChapter.Clear();
            }
        }
    }

    private static IEnumerable<ScriptureDataSource> GetScriptureDataSources()
    {
        foreach (var fileName in new[] { "nasb1995.json", "asv.json" })
        {
            var label = fileName.Equals("nasb1995.json", StringComparison.OrdinalIgnoreCase)
                ? "NASB1995"
                : "ASV";

            yield return new ScriptureDataSource(
                label,
                Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", fileName));
            yield return new ScriptureDataSource(label, Path.Combine(AppContext.BaseDirectory, "Data", fileName));
            yield return new ScriptureDataSource(label, Path.Combine(AppContext.BaseDirectory, fileName));

            var cursor = new DirectoryInfo(AppContext.BaseDirectory);
            while (cursor is not null)
            {
                yield return new ScriptureDataSource(label, Path.Combine(cursor.FullName, fileName));
                cursor = cursor.Parent;
            }

            yield return new ScriptureDataSource(
                label,
                Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Bible Study Studio",
                    fileName));
        }
    }

    private void LoadTagntData()
    {
        _tagntEntriesByReference.Clear();

        var loaded = false;
        foreach (var path in GetTagntDataPaths())
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                ParseTagntRows(File.ReadAllText(path));
                loaded = true;
            }
            catch
            {
                // Keep the scripture panel usable even if a downloaded TAGNT file has an unexpected row.
            }
        }

        if (!loaded)
        {
            ParseTagntRows(GetBuiltInTagntSeed());
        }
    }

    private static IEnumerable<string> GetTagntDataPaths()
    {
        foreach (var fileName in new[] { "tagnt-mat-jhn.txt", "tagnt-act-rev.txt", "TAGNT Mat-Jhn.txt", "TAGNT Act-Rev.txt" })
        {
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", fileName);
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", "Bible Study App", "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, fileName);
        }
    }

    private void LoadGreekLexiconData()
    {
        _greekLexiconByStrong.Clear();
        _greekLexiconLoaded = true;

        foreach (var path in GetGreekLexiconDataPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                ParseGreekLexiconRows(File.ReadLines(path));
            }
            catch
            {
                // Lexicon data is additive; a bad file should not break the scripture reader.
            }
        }
    }

    private static IEnumerable<string> GetGreekLexiconDataPaths()
    {
        foreach (var fileName in new[]
        {
            "tbesg.txt",
            "TBESG.txt",
            "TBESG - Translators Brief lexicon of Extended Strongs for Greek.txt"
        })
        {
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", fileName);
            yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", "Bible Study App", "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, "Data", fileName);
            yield return Path.Combine(AppContext.BaseDirectory, fileName);
        }

        foreach (var directory in GetDataDirectories())
        {
            if (!Directory.Exists(directory))
            {
                continue;
            }

            foreach (var path in Directory.EnumerateFiles(directory, "*TBESG*.txt"))
            {
                yield return path;
            }
        }
    }

    private static IEnumerable<string> GetDataDirectories()
    {
        yield return @"C:\Users\jake1\OneDrive\Desktop\Bible Study App";
        yield return Path.Combine(@"C:\Users\jake1\OneDrive\Desktop\Bible Study App", "Bible Study App", "Data");
        yield return Path.Combine(AppContext.BaseDirectory, "Data");
        yield return AppContext.BaseDirectory;
    }

    private void ParseGreekLexiconRows(IEnumerable<string> lines)
    {
        foreach (var line in lines)
        {
            if (!TryParseGreekLexiconLine(line, out var entry))
            {
                continue;
            }

            CacheGreekLexiconEntry(entry);
        }
    }

    private static bool TryParseGreekLexiconLine(string line, out GreekLexiconEntry entry)
    {
        entry = EmptyGreekLexiconEntry;
        if (string.IsNullOrWhiteSpace(line) || !line.StartsWith("G", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var cells = line.Split('\t');
        if (cells.Length < 8 || !Regex.IsMatch(cells[0].Trim(), @"^G\d{4,5}[A-Z]?$", RegexOptions.CultureInvariant))
        {
            return false;
        }

        entry = new GreekLexiconEntry(
            cells[0].Trim(),
            cells[1].Trim(),
            cells[2].Trim(),
            cells[3].Trim(),
            cells[4].Trim(),
            cells[5].Trim(),
            cells[6].Trim(),
            CleanLexiconMarkup(cells[7].Trim()));
        return true;
    }

    private void CacheGreekLexiconEntry(GreekLexiconEntry entry)
    {
        foreach (var strong in GetLexiconKeys(entry))
        {
            _greekLexiconByStrong.TryAdd(strong, entry);
        }
    }

    private static IEnumerable<string> GetLexiconKeys(GreekLexiconEntry entry)
    {
        foreach (var value in new[] { entry.EStrong, entry.DStrong, entry.UStrong })
        {
            foreach (var strong in ExtractStrongCandidates(value))
            {
                yield return strong;
            }
        }
    }

    private static IEnumerable<string> ExtractStrongCandidates(params string[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            foreach (Match match in Regex.Matches(value, @"G\d{4,5}[A-Z]?", RegexOptions.CultureInvariant))
            {
                yield return match.Value;
            }
        }
    }

    private GreekLexiconEntry? FindGreekLexiconEntry(TagntWordEntry entry)
    {
        var candidates = ExtractStrongCandidates(entry.DStrong, entry.SimpleStrong, entry.AltStrongs)
            .SelectMany(GetStrongLookupCandidates)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        foreach (var candidate in candidates)
        {
            if (_greekLexiconByStrong.TryGetValue(candidate, out var exactMatch))
            {
                return exactMatch;
            }
        }

        var searchedEntry = FindGreekLexiconEntryInFiles(candidates);
        if (searchedEntry is not null)
        {
            CacheGreekLexiconEntry(searchedEntry);
        }

        return searchedEntry;
    }

    private GreekLexiconEntry? FindGreekLexiconEntryInFiles(IReadOnlyCollection<string> candidates)
    {
        if (candidates.Count == 0)
        {
            return null;
        }

        foreach (var path in GetGreekLexiconDataPaths().Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            try
            {
                foreach (var line in File.ReadLines(path))
                {
                    if (!TryParseGreekLexiconLine(line, out var entry))
                    {
                        continue;
                    }

                    if (GetLexiconKeys(entry).Any(key => candidates.Contains(key, StringComparer.OrdinalIgnoreCase)))
                    {
                        return entry;
                    }
                }
            }
            catch
            {
                // Lexicon lookup is best-effort; a bad file should not break Strong's tagging.
            }
        }

        return null;
    }

    private static IEnumerable<string> GetStrongLookupCandidates(string strong)
    {
        if (string.IsNullOrWhiteSpace(strong))
        {
            yield break;
        }

        yield return strong;

        var baseStrong = Regex.Match(strong, @"^G\d{4,5}", RegexOptions.CultureInvariant).Value;
        if (!string.IsNullOrWhiteSpace(baseStrong) && !baseStrong.Equals(strong, StringComparison.OrdinalIgnoreCase))
        {
            yield return baseStrong;
        }
    }

    private void EnsureGreekLexiconDataLoaded()
    {
        if (_greekLexiconLoaded)
        {
            return;
        }

        LoadGreekLexiconData();
    }

    private static string CleanLexiconMarkup(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleaned = value
            .Replace("<BR />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<BR/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<BR>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br/>", "\n", StringComparison.OrdinalIgnoreCase)
            .Replace("<br>", "\n", StringComparison.OrdinalIgnoreCase);
        cleaned = Regex.Replace(cleaned, @"<ref='([^']+)'>(.*?)</ref>", "$2", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, "<.*?>", string.Empty, RegexOptions.CultureInvariant);
        cleaned = WebUtility.HtmlDecode(cleaned);
        cleaned = Regex.Replace(cleaned, @"[ \t]+", " ", RegexOptions.CultureInvariant);
        cleaned = Regex.Replace(cleaned, @"\n{3,}", "\n\n", RegexOptions.CultureInvariant);
        return cleaned.Trim();
    }

    private void ParseTagntRows(string rawText)
    {
        List<TagntWordEntry>? currentEntries = null;

        foreach (var rawLine in rawText.Split(["\r\n", "\n"], StringSplitOptions.None))
        {
            var line = rawLine.TrimEnd('\r');
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split('\t');
            if (cells.Length == 0)
            {
                continue;
            }

            var label = cells[0].Trim();
            if (TryApplyTagntWordDetailRow(cells))
            {
                continue;
            }

            if (label.StartsWith("# ", StringComparison.Ordinal))
            {
                currentEntries = CreateTagntEntries(label[2..].Trim(), cells.Skip(1));
                continue;
            }

            if (currentEntries is null || !label.StartsWith("#_", StringComparison.Ordinal))
            {
                continue;
            }

            ApplyTagntRow(currentEntries, label[2..].Trim(), cells.Skip(1).ToArray());
        }
    }

    private List<TagntWordEntry>? CreateTagntEntries(string referenceText, IEnumerable<string> greekCells)
    {
        if (!TryParseTagntReference(referenceText, out var key))
        {
            return null;
        }

        var entries = greekCells
            .Select((cell, index) => new TagntWordEntry(index + 1, cell.Trim()))
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Greek))
            .ToList();
        if (entries.Count == 0)
        {
            return null;
        }

        _tagntEntriesByReference[key] = entries;
        return entries;
    }

    private static void ApplyTagntRow(List<TagntWordEntry> entries, string label, IReadOnlyList<string> values)
    {
        var normalizedLabel = label.Replace(" ", string.Empty, StringComparison.OrdinalIgnoreCase).ToLowerInvariant();
        for (var index = 0; index < entries.Count && index < values.Count; index++)
        {
            var value = values[index].Trim();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            switch (normalizedLabel)
            {
                case "translation":
                case "english":
                    entries[index].English = value;
                    break;
                case "word=grammar":
                    ApplyTagntStrongGrammar(entries[index], value);
                    break;
                case "dstrong":
                case "strong":
                case "strongs":
                    entries[index].DStrong = value;
                    break;
                case "grammar":
                case "morphology":
                    entries[index].Grammar = value;
                    break;
                case "dictionaryform&gloss":
                case "dictionaryform":
                    entries[index].DictionaryForm = value;
                    break;
                case "gloss":
                case "sub-meanings":
                case "submeanings":
                    entries[index].Gloss = value;
                    break;
                case "wordtype":
                    entries[index].WordType = value;
                    break;
                case "editions":
                    entries[index].Editions = value;
                    break;
                case "variants":
                case "variantnotes":
                    entries[index].VariantNotes = value;
                    break;
            }
        }
    }

    private bool TryApplyTagntWordDetailRow(IReadOnlyList<string> cells)
    {
        if (cells.Count < 4)
        {
            return false;
        }

        var match = Regex.Match(
            cells[0].Trim(),
            @"^(?<book>[1-3]?[A-Za-z]{2,3})\.(?<chapter>\d+)\.(?<verse>\d+)#(?<word>\d+)=(?<type>[^\t]+)$",
            RegexOptions.CultureInvariant);
        if (!match.Success
            || !TryMapTagntBookCode(match.Groups["book"].Value, out var bookName)
            || !int.TryParse(match.Groups["chapter"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chapter)
            || !int.TryParse(match.Groups["verse"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var verse)
            || !int.TryParse(match.Groups["word"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var wordIndex))
        {
            return false;
        }

        var key = new ScriptureReferenceKey(bookName, chapter, verse);
        if (!_tagntEntriesByReference.TryGetValue(key, out var entries))
        {
            entries = new List<TagntWordEntry>();
            _tagntEntriesByReference[key] = entries;
        }

        var entry = entries.FirstOrDefault(candidate => candidate.WordIndex == wordIndex);
        if (entry is null)
        {
            entry = new TagntWordEntry(wordIndex, cells.Count > 1 ? cells[1].Trim() : string.Empty);
            entries.Add(entry);
            entries.Sort((left, right) => left.WordIndex.CompareTo(right.WordIndex));
        }
        else if (string.IsNullOrWhiteSpace(entry.Greek) && cells.Count > 1)
        {
            entry.Greek = cells[1].Trim();
        }

        entry.WordType = match.Groups["type"].Value.Trim();
        if (cells.Count > 2)
        {
            entry.English = cells[2].Trim();
        }

        if (cells.Count > 3)
        {
            ApplyTagntStrongGrammar(entry, cells[3].Trim());
        }

        if (cells.Count > 4)
        {
            ApplyDictionaryGloss(entry, cells[4].Trim());
        }

        if (cells.Count > 5)
        {
            entry.Editions = cells[5].Trim();
        }

        entry.VariantNotes = string.Join(
            "\n",
            new[] { CellAt(cells, 6), CellAt(cells, 7) }.Where(value => !string.IsNullOrWhiteSpace(value)));
        entry.SubMeaning = CellAt(cells, 9);
        entry.ConjoinedWord = CellAt(cells, 10);
        entry.SimpleStrong = CellAt(cells, 11);
        entry.AltStrongs = CellAt(cells, 12);
        return true;
    }

    private static string CellAt(IReadOnlyList<string> cells, int index)
    {
        return index < cells.Count
            ? cells[index].Trim()
            : string.Empty;
    }

    private static void ApplyTagntStrongGrammar(TagntWordEntry entry, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var splitIndex = value.LastIndexOf('=');
        if (splitIndex > 0 && splitIndex < value.Length - 1)
        {
            entry.DStrong = value[..splitIndex].Trim();
            entry.Grammar = value[(splitIndex + 1)..].Trim();
            return;
        }

        entry.DStrong = value.Trim();
    }

    private static void ApplyDictionaryGloss(TagntWordEntry entry, string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        var splitIndex = value.IndexOf('=');
        if (splitIndex > 0 && splitIndex < value.Length - 1)
        {
            entry.DictionaryForm = value[..splitIndex].Trim();
            entry.Gloss = value[(splitIndex + 1)..].Trim();
            return;
        }

        entry.Gloss = value.Trim();
    }

    private static bool TryParseTagntReference(string referenceText, out ScriptureReferenceKey key)
    {
        key = default;
        var parts = referenceText.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3
            || !TryMapTagntBookCode(parts[0], out var bookName)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var chapter)
            || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var verse))
        {
            return false;
        }

        key = new ScriptureReferenceKey(bookName, chapter, verse);
        return true;
    }

    private static bool TryMapTagntBookCode(string code, out string bookName)
    {
        var normalized = code.Trim();
        var books = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Mat"] = "Matthew",
            ["Mrk"] = "Mark",
            ["Mar"] = "Mark",
            ["Luk"] = "Luke",
            ["Jhn"] = "John",
            ["Joh"] = "John",
            ["Act"] = "Acts",
            ["Rom"] = "Romans",
            ["1Co"] = "1 Corinthians",
            ["2Co"] = "2 Corinthians",
            ["Gal"] = "Galatians",
            ["Eph"] = "Ephesians",
            ["Php"] = "Philippians",
            ["Col"] = "Colossians",
            ["1Th"] = "1 Thessalonians",
            ["2Th"] = "2 Thessalonians",
            ["1Ti"] = "1 Timothy",
            ["2Ti"] = "2 Timothy",
            ["Tit"] = "Titus",
            ["Phm"] = "Philemon",
            ["Heb"] = "Hebrews",
            ["Jas"] = "James",
            ["1Pe"] = "1 Peter",
            ["2Pe"] = "2 Peter",
            ["1Jn"] = "1 John",
            ["2Jn"] = "2 John",
            ["3Jn"] = "3 John",
            ["Jud"] = "Jude",
            ["Rev"] = "Revelation"
        };

        return books.TryGetValue(normalized, out bookName!);
    }

    private static string GetBuiltInTagntSeed()
    {
        return "# Mat.1.1\t\u0392\u03af\u03b2\u03bb\u03bf\u03c2\t\u03b3\u03b5\u03bd\u03ad\u03c3\u03b5\u03c9\u03c2\t\u1f38\u03b7\u03c3\u03bf\u1fe6\t\u03a7\u03c1\u03b9\u03c3\u03c4\u03bf\u1fe6\t\u03c5\u1f31\u03bf\u1fe6\t\u0394\u03b1\u03c5\u1f76\u03b4\t\u03c5\u1f31\u03bf\u1fe6\t\u1f08\u03b2\u03c1\u03b1\u1f71\u03bc.\n"
            + "#_Translation\t[The] book\tof [the] genealogy\tof Jesus\tChrist\tson\tof David\tson\tof Abraham.";
    }

    private void RenderScripturePanel(WorkspaceItem study, int? requestedChapter = null, bool scrollToSelection = true)
    {
        var root = GetRootWorkspace(study);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var startChapter = study.PassageStartChapter ?? 1;
        var maxChapter = Math.Max(1, book?.Chapters ?? 1);
        var chapter = Math.Clamp(requestedChapter ?? startChapter, 1, maxChapter);
        var startVerse = study.PassageStartVerse;
        var endChapter = study.PassageEndChapter ?? chapter;
        var endVerse = study.PassageEndVerse;
        var verseCount = GetVerseCount(book, chapter);

        _scriptureVisibleBookName = root.Name;
        _scriptureVisibleChapter = chapter;
        ScripturePanelTitle.Text = $"{root.Name} - Chapter {chapter}";
        ScripturePanelSubtitle.Text = $"{_scriptureTranslationLabel} | Selected passage: {study.PassageLabel}";
        ScripturePreviousChapterButton.Visibility = chapter > 1 ? Visibility.Visible : Visibility.Collapsed;
        ScriptureNextChapterButton.Visibility = chapter < maxChapter ? Visibility.Visible : Visibility.Collapsed;
        _scriptureVerses.Clear();
        _scriptureParagraphsByVerse.Clear();
        ScriptureDocument.Blocks.Clear();

        if (!TryGetChapterVerses(root.Name, chapter, out var verses))
        {
            ScriptureScrollViewer.Visibility = Visibility.Collapsed;
            ScriptureSelectionRail.Visibility = Visibility.Collapsed;
            ScriptureSelectionMarker.Visibility = Visibility.Collapsed;
            ScriptureMissingText.Visibility = Visibility.Visible;
            ScriptureMissingText.Text =
                "Scripture text is not configured yet.\n\nPlace nasb1995.json or asv.json at the project root or:\n"
                + string.Join("\n", GetScriptureDataSources().Take(6).Select(dataSource => dataSource.Path));
            return;
        }

        ScriptureMissingText.Visibility = Visibility.Collapsed;
        ScriptureScrollViewer.Visibility = Visibility.Visible;

        for (var index = 0; index < Math.Min(verseCount, verses.Count); index++)
        {
            var verseNumber = index + 1;
            _scriptureVerses.Add(new ScriptureVerseDisplay(
                verseNumber,
                verses[index],
                IsVerseSelected(chapter, verseNumber, startChapter, startVerse, endChapter, endVerse)));
        }

        RenderScriptureDocument();

        Dispatcher.BeginInvoke(() =>
        {
            if (scrollToSelection && _scriptureVerses.Any(verse => verse.IsSelected))
            {
                ScrollScriptureToSelectedVerse();
            }
            else
            {
                ScriptureScrollViewer.ScrollToTop();
            }

            UpdateScriptureSelectionMarker();
        }, DispatcherPriority.Background);
    }

    private bool TryGetChapterVerses(string bookName, int chapter, out List<string> verses)
    {
        verses = new List<string>();
        if (!_scriptureTextByBookChapter.TryGetValue(bookName, out var chapters)
            || !chapters.TryGetValue(chapter.ToString(), out var chapterVerses))
        {
            return false;
        }

        verses = chapterVerses;
        return true;
    }

    private static bool IsVerseSelected(
        int visibleChapter,
        int visibleVerse,
        int startChapter,
        int? startVerse,
        int endChapter,
        int? endVerse)
    {
        if (startVerse is null)
        {
            return false;
        }

        var effectiveEndVerse = endVerse ?? startVerse.Value;
        var afterStart = visibleChapter > startChapter
            || (visibleChapter == startChapter && visibleVerse >= startVerse.Value);
        var beforeEnd = visibleChapter < endChapter
            || (visibleChapter == endChapter && visibleVerse <= effectiveEndVerse);
        return afterStart && beforeEnd;
    }

    private void ScripturePreviousChapter_Click(object sender, RoutedEventArgs e)
    {
        NavigateScriptureChapter(-1);
    }

    private void ScriptureNextChapter_Click(object sender, RoutedEventArgs e)
    {
        NavigateScriptureChapter(1);
    }

    private void NavigateScriptureChapter(int direction)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var root = GetRootWorkspace(_currentStudy);
        var book = BibleBooks.FirstOrDefault(candidate => candidate.Name == root.Name);
        var maxChapter = Math.Max(1, book?.Chapters ?? 1);
        var currentChapter = _scriptureVisibleChapter ?? _currentStudy.PassageStartChapter ?? 1;
        var nextChapter = Math.Clamp(currentChapter + direction, 1, maxChapter);
        if (nextChapter == currentChapter)
        {
            return;
        }

        RenderScripturePanel(_currentStudy, nextChapter, scrollToSelection: false);
    }

    private void ScriptureScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_strongsEnabled)
        {
            return;
        }

        UpdateScriptureSelectionMarker();
    }

    private void ScriptureScrollViewer_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_strongsEnabled)
        {
            return;
        }

        UpdateScriptureSelectionMarker();
    }

    private void ScriptureScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _scriptureSmoothScrollActive
            ? _scriptureSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        var scrollSpeed = _strongsEnabled ? 1.05 : 0.55;
        _scriptureSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * scrollSpeed),
            0,
            scrollViewer.ScrollableHeight);
        StartScriptureSmoothScroll();
        e.Handled = true;
    }

    private void StartScriptureSmoothScroll()
    {
        if (_scriptureSmoothScrollActive)
        {
            return;
        }

        _scriptureSmoothScrollActive = true;
        _scriptureSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += ScriptureSmoothScroll_Rendering;
    }

    private void StopScriptureSmoothScroll()
    {
        if (!_scriptureSmoothScrollActive)
        {
            return;
        }

        _scriptureSmoothScrollActive = false;
        CompositionTarget.Rendering -= ScriptureSmoothScroll_Rendering;
    }

    private void ScriptureSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _scriptureSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _scriptureSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_scriptureSmoothScrollTargetOffset, 0, ScriptureScrollViewer.ScrollableHeight);
        var distance = targetOffset - ScriptureScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            ScriptureScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopScriptureSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        ScriptureScrollViewer.ScrollToVerticalOffset(ScriptureScrollViewer.VerticalOffset + (distance * progress));
    }

    private void BibleBooksScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _bibleBooksSmoothScrollActive
            ? _bibleBooksSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _bibleBooksSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartBibleBooksSmoothScroll();
        e.Handled = true;
    }

    private void StartBibleBooksSmoothScroll()
    {
        if (_bibleBooksSmoothScrollActive)
        {
            return;
        }

        _bibleBooksSmoothScrollActive = true;
        _bibleBooksSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += BibleBooksSmoothScroll_Rendering;
    }

    private void StopBibleBooksSmoothScroll()
    {
        if (!_bibleBooksSmoothScrollActive)
        {
            return;
        }

        _bibleBooksSmoothScrollActive = false;
        CompositionTarget.Rendering -= BibleBooksSmoothScroll_Rendering;
    }

    private void BibleBooksSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _bibleBooksSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _bibleBooksSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_bibleBooksSmoothScrollTargetOffset, 0, BibleBooksScrollViewer.ScrollableHeight);
        var distance = targetOffset - BibleBooksScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            BibleBooksScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopBibleBooksSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        BibleBooksScrollViewer.ScrollToVerticalOffset(BibleBooksScrollViewer.VerticalOffset + (distance * progress));
    }

    private void StudyEditorScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _studyEditorSmoothScrollActive
            ? _studyEditorSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _studyEditorSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartStudyEditorSmoothScroll();
        e.Handled = true;
    }

    private void StartStudyEditorSmoothScroll()
    {
        if (_studyEditorSmoothScrollActive)
        {
            return;
        }

        _studyEditorSmoothScrollActive = true;
        _studyEditorSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += StudyEditorSmoothScroll_Rendering;
    }

    private void StopStudyEditorSmoothScroll()
    {
        if (!_studyEditorSmoothScrollActive)
        {
            return;
        }

        _studyEditorSmoothScrollActive = false;
        CompositionTarget.Rendering -= StudyEditorSmoothScroll_Rendering;
    }

    private void StudyEditorSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _studyEditorSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _studyEditorSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_studyEditorSmoothScrollTargetOffset, 0, StudyEditorScrollViewer.ScrollableHeight);
        var distance = targetOffset - StudyEditorScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            StudyEditorScrollViewer.ScrollToVerticalOffset(targetOffset);
            UpdateSmoothEditorCaret(animate: false);
            StopStudyEditorSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        StudyEditorScrollViewer.ScrollToVerticalOffset(StudyEditorScrollViewer.VerticalOffset + (distance * progress));
        UpdateSmoothEditorCaret(animate: false);
    }

    private void TodayScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        if (FindAncestor<ScrollViewer>(e.OriginalSource as DependencyObject) is { } nestedScrollViewer
            && !ReferenceEquals(nestedScrollViewer, scrollViewer)
            && nestedScrollViewer.ScrollableHeight > 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _todaySmoothScrollActive
            ? _todaySmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _todaySmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartTodaySmoothScroll();
        e.Handled = true;
    }

    private void StartTodaySmoothScroll()
    {
        if (_todaySmoothScrollActive)
        {
            return;
        }

        _todaySmoothScrollActive = true;
        _todaySmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += TodaySmoothScroll_Rendering;
    }

    private void StopTodaySmoothScroll()
    {
        if (!_todaySmoothScrollActive)
        {
            return;
        }

        _todaySmoothScrollActive = false;
        CompositionTarget.Rendering -= TodaySmoothScroll_Rendering;
    }

    private void TodaySmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _todaySmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _todaySmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_todaySmoothScrollTargetOffset, 0, TodayScrollViewer.ScrollableHeight);
        var distance = targetOffset - TodayScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            TodayScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopTodaySmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        TodayScrollViewer.ScrollToVerticalOffset(TodayScrollViewer.VerticalOffset + (distance * progress));
    }

    private void ScheduledMessagesScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _scheduledMessagesSmoothScrollActive
            ? _scheduledMessagesSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _scheduledMessagesSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartScheduledMessagesSmoothScroll();
        e.Handled = true;
    }

    private void StartScheduledMessagesSmoothScroll()
    {
        if (_scheduledMessagesSmoothScrollActive)
        {
            return;
        }

        _scheduledMessagesSmoothScrollActive = true;
        _scheduledMessagesSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += ScheduledMessagesSmoothScroll_Rendering;
    }

    private void StopScheduledMessagesSmoothScroll()
    {
        if (!_scheduledMessagesSmoothScrollActive)
        {
            return;
        }

        _scheduledMessagesSmoothScrollActive = false;
        CompositionTarget.Rendering -= ScheduledMessagesSmoothScroll_Rendering;
    }

    private void ScheduledMessagesSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _scheduledMessagesSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _scheduledMessagesSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_scheduledMessagesSmoothScrollTargetOffset, 0, ScheduledMessagesScrollViewer.ScrollableHeight);
        var distance = targetOffset - ScheduledMessagesScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            ScheduledMessagesScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopScheduledMessagesSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        ScheduledMessagesScrollViewer.ScrollToVerticalOffset(ScheduledMessagesScrollViewer.VerticalOffset + (distance * progress));
    }

    private void StrongsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer scrollViewer || scrollViewer.ScrollableHeight <= 0)
        {
            return;
        }

        RevealScrollBarsWhileScrolling(scrollViewer);

        var currentTarget = _strongsSmoothScrollActive
            ? _strongsSmoothScrollTargetOffset
            : scrollViewer.VerticalOffset;
        _strongsSmoothScrollTargetOffset = Math.Clamp(
            currentTarget - (e.Delta * 0.55),
            0,
            scrollViewer.ScrollableHeight);
        StartStrongsSmoothScroll();
        e.Handled = true;
    }

    private void StartStrongsSmoothScroll()
    {
        if (_strongsSmoothScrollActive)
        {
            return;
        }

        _strongsSmoothScrollActive = true;
        _strongsSmoothScrollLastFrame = DateTime.UtcNow;
        CompositionTarget.Rendering += StrongsSmoothScroll_Rendering;
    }

    private void StopStrongsSmoothScroll()
    {
        if (!_strongsSmoothScrollActive)
        {
            return;
        }

        _strongsSmoothScrollActive = false;
        CompositionTarget.Rendering -= StrongsSmoothScroll_Rendering;
    }

    private void StrongsSmoothScroll_Rendering(object? sender, EventArgs e)
    {
        var now = DateTime.UtcNow;
        var elapsedSeconds = Math.Clamp((now - _strongsSmoothScrollLastFrame).TotalSeconds, 0.001, 0.05);
        _strongsSmoothScrollLastFrame = now;

        var targetOffset = Math.Clamp(_strongsSmoothScrollTargetOffset, 0, StrongsScrollViewer.ScrollableHeight);
        var distance = targetOffset - StrongsScrollViewer.VerticalOffset;
        if (Math.Abs(distance) < 0.5)
        {
            StrongsScrollViewer.ScrollToVerticalOffset(targetOffset);
            StopStrongsSmoothScroll();
            return;
        }

        var progress = 1 - Math.Exp(-18 * elapsedSeconds);
        StrongsScrollViewer.ScrollToVerticalOffset(StrongsScrollViewer.VerticalOffset + (distance * progress));
    }

    private void RevealScrollBarsWhileScrolling(ScrollViewer scrollViewer)
    {
        SetScrollBarScrollingState(scrollViewer, true);

        if (!_scrollBarRevealTimers.TryGetValue(scrollViewer, out var timer))
        {
            timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(1400)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                SetScrollBarScrollingState(scrollViewer, false);
            };
            _scrollBarRevealTimers[scrollViewer] = timer;
        }

        timer.Stop();
        timer.Start();
    }

    private static void SetScrollBarScrollingState(DependencyObject root, bool isScrolling)
    {
        foreach (var scrollBar in FindOwnedScrollBars(root))
        {
            scrollBar.Tag = isScrolling ? "Scrolling" : null;
        }
    }

    private void StopScrollBarRevealTimers()
    {
        foreach (var timer in _scrollBarRevealTimers.Values)
        {
            timer.Stop();
        }

        _scrollBarRevealTimers.Clear();
    }

    private static IEnumerable<T> FindDescendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var descendant in FindDescendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<ScrollBar> FindOwnedScrollBars(DependencyObject root)
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            var child = VisualTreeHelper.GetChild(root, index);
            if (child is ScrollBar scrollBar)
            {
                yield return scrollBar;
                continue;
            }

            if (child is ScrollViewer && !ReferenceEquals(child, root))
            {
                continue;
            }

            foreach (var descendant in FindOwnedScrollBars(child))
            {
                yield return descendant;
            }
        }
    }

    private static T? FindAncestor<T>(DependencyObject? source)
        where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match)
            {
                return match;
            }

            source = GetTraversalParent(source);
        }

        return null;
    }

    private static bool IsDescendantOf(DependencyObject? source, DependencyObject ancestor)
    {
        while (source is not null)
        {
            if (ReferenceEquals(source, ancestor))
            {
                return true;
            }

            source = GetTraversalParent(source);
        }

        return false;
    }

    private static DependencyObject? GetTraversalParent(DependencyObject source)
    {
        return source switch
        {
            Visual or System.Windows.Media.Media3D.Visual3D => VisualTreeHelper.GetParent(source),
            FrameworkContentElement frameworkContent => frameworkContent.Parent ?? ContentOperations.GetParent(frameworkContent),
            ContentElement content => ContentOperations.GetParent(content),
            _ => LogicalTreeHelper.GetParent(source)
        };
    }

    private void ScriptureFontSizeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (ScriptureFontSizeText is null)
        {
            return;
        }

        ScriptureFontSizeText.Text = Math.Round(e.NewValue).ToString(CultureInfo.InvariantCulture);
        if (ScriptureDocument is not null)
        {
            ScriptureDocument.FontSize = e.NewValue;
            foreach (var block in ScriptureDocument.Blocks)
            {
                block.FontSize = e.NewValue;
            }
        }
        if (ScripturePanelTitle is not null)
        {
            ScripturePanelTitle.FontSize = e.NewValue + 4;
        }

        if (!_strongsEnabled)
        {
            Dispatcher.BeginInvoke(() => UpdateScriptureSelectionMarker(), DispatcherPriority.Background);
        }
    }

    private void ScrollScriptureToSelectedVerse()
    {
        var firstSelected = _scriptureVerses.FirstOrDefault(verse => verse.IsSelected)
            ?? _scriptureVerses.FirstOrDefault();
        if (firstSelected is null)
        {
            return;
        }

        if (_scriptureParagraphsByVerse.TryGetValue(firstSelected.VerseNumber, out var paragraph))
        {
            paragraph.BringIntoView();
        }
    }

    private void UpdateScriptureSelectionMarker()
    {
        var selectedVerses = _scriptureVerses.Where(verse => verse.IsSelected).ToList();
        if (selectedVerses.Count == 0 || _scriptureVerses.Count == 0 || ScriptureSelectionRail.ActualHeight <= 0)
        {
            ScriptureSelectionRail.Visibility = Visibility.Collapsed;
            ScriptureSelectionMarker.Visibility = Visibility.Collapsed;
            return;
        }

        var selectedParagraphs = selectedVerses
            .Select(verse => _scriptureParagraphsByVerse.TryGetValue(verse.VerseNumber, out var paragraph) ? paragraph : null)
            .Where(paragraph => paragraph is not null)
            .Cast<Paragraph>()
            .ToList();
        if (selectedParagraphs.Count == 0 || ScriptureTextView.ActualHeight <= 0)
        {
            Dispatcher.BeginInvoke(() => UpdateScriptureSelectionMarker(), DispatcherPriority.Background);
            return;
        }

        var selectedTop = selectedParagraphs.Min(paragraph => paragraph.ContentStart.GetCharacterRect(LogicalDirection.Forward).Y);
        var selectedBottom = selectedParagraphs.Max(paragraph => paragraph.ContentEnd.GetCharacterRect(LogicalDirection.Backward).Bottom);
        var contentHeight = Math.Max(1, ScriptureTextView.ExtentHeight > 0 ? ScriptureTextView.ExtentHeight : ScriptureTextView.ActualHeight);
        var railHeight = ScriptureSelectionRail.ActualHeight;
        var markerTopRatio = Math.Clamp(selectedTop / contentHeight, 0, 1);
        var markerHeightRatio = Math.Clamp((selectedBottom - selectedTop) / contentHeight, 0.01, 1);
        var markerHeight = Math.Max(18, railHeight * markerHeightRatio);
        var markerTop = Math.Min(Math.Max(0, railHeight * markerTopRatio), Math.Max(0, railHeight - markerHeight));

        ScriptureSelectionRail.Visibility = Visibility.Visible;
        ScriptureSelectionMarker.Visibility = Visibility.Visible;
        ScriptureSelectionMarker.Height = markerHeight;
        Canvas.SetTop(ScriptureSelectionMarker, markerTop);
    }

    private void RenderScriptureDocument()
    {
        _scriptureParagraphsByVerse.Clear();
        ScriptureDocument.Blocks.Clear();
        ScriptureDocument.Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17));
        ScriptureDocument.FontSize = ScriptureFontSizeSlider.Value;

        foreach (var verse in _scriptureVerses)
        {
            var paragraph = new Paragraph
            {
                Margin = new Thickness(0, 0, 0, 3),
                Padding = new Thickness(6, 4, 6, 4),
                Background = _strongsEnabled
                    ? Brushes.Transparent
                    : CreateAnimatedVerseHighlightBrush(verse.IsSelected)
            };

            paragraph.Inlines.Add(new Run(verse.VerseNumber.ToString(CultureInfo.InvariantCulture))
            {
                FontWeight = FontWeights.Black,
                BaselineAlignment = BaselineAlignment.Superscript,
                FontSize = 11
            });
            paragraph.Inlines.Add(new Run(" "));
            if (_strongsEnabled
                && _scriptureVisibleBookName is not null
                && _scriptureVisibleChapter is { } visibleChapter
                && _tagntEntriesByReference.TryGetValue(
                    new ScriptureReferenceKey(_scriptureVisibleBookName, visibleChapter, verse.VerseNumber),
                    out var tagntEntries))
            {
                AddTaggedScriptureRuns(paragraph, verse, tagntEntries);
            }
            else
            {
                paragraph.Inlines.Add(new Run(verse.Text));
            }

            ScriptureDocument.Blocks.Add(paragraph);
            _scriptureParagraphsByVerse[verse.VerseNumber] = paragraph;
        }

        ApplySavedRichTextAnnotations(ScriptureTextView);
    }

    private void AddTaggedScriptureRuns(Paragraph paragraph, ScriptureVerseDisplay verse, IReadOnlyList<TagntWordEntry> tagntEntries)
    {
        var matches = FindTagntMatches(verse.Text, tagntEntries);
        if (matches.Count == 0)
        {
            paragraph.Inlines.Add(new Run(verse.Text));
            return;
        }

        var cursor = 0;
        var linkIndex = 0;
        foreach (var match in matches.OrderBy(match => match.Start))
        {
            if (match.Start < cursor)
            {
                continue;
            }

            if (match.Start > cursor)
            {
                paragraph.Inlines.Add(new Run(verse.Text[cursor..match.Start]));
            }

            var phrase = verse.Text.Substring(match.Start, match.Length);
            var link = new Span(new Run(phrase))
            {
                Cursor = Cursors.Hand,
                Foreground = GetStrongsLinkBrush(linkIndex),
                FontWeight = FontWeights.SemiBold,
                Tag = new StrongsSelection(_scriptureVisibleBookName ?? string.Empty, _scriptureVisibleChapter ?? 0, verse.VerseNumber, phrase, match.Entry)
            };
            link.MouseLeftButtonDown += StrongsPhrase_Click;
            paragraph.Inlines.Add(link);
            cursor = match.Start + match.Length;
            linkIndex++;
        }

        if (cursor < verse.Text.Length)
        {
            paragraph.Inlines.Add(new Run(verse.Text[cursor..]));
        }
    }

    private Brush GetStrongsLinkBrush(int index)
    {
        return (index % 3) switch
        {
            0 => GetResourceBrush("Coral"),
            1 => GetResourceBrush("PanelBackground"),
            _ => GetResourceBrush("Mint")
        };
    }

    private static List<TagntTextMatch> FindTagntMatches(string verseText, IReadOnlyList<TagntWordEntry> tagntEntries)
    {
        var matches = new List<TagntTextMatch>();
        foreach (var entry in tagntEntries)
        {
            foreach (var phrase in BuildEnglishPhraseCandidates(entry.English))
            {
                var match = Regex.Match(
                    verseText,
                    $@"(?<!\w){Regex.Escape(phrase)}(?!\w)",
                    RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                if (!match.Success || matches.Any(existing => RangesOverlap(existing.Start, existing.Length, match.Index, match.Length)))
                {
                    continue;
                }

                matches.Add(new TagntTextMatch(match.Index, match.Length, entry));
                break;
            }
        }

        return matches;
    }

    private static IEnumerable<string> BuildEnglishPhraseCandidates(string phrase)
    {
        if (string.IsNullOrWhiteSpace(phrase))
        {
            yield break;
        }

        var cleaned = phrase.Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal)
            .Trim();
        cleaned = Regex.Replace(cleaned, @"\s+", " ");
        cleaned = cleaned.Trim(' ', '.', ',', ';', ':', '!', '?');
        if (!string.IsNullOrWhiteSpace(cleaned))
        {
            yield return cleaned;
        }

        var withoutBracketed = Regex.Replace(phrase, @"\[[^\]]+\]", string.Empty);
        withoutBracketed = Regex.Replace(withoutBracketed, @"\s+", " ").Trim(' ', '.', ',', ';', ':', '!', '?');
        if (!string.IsNullOrWhiteSpace(withoutBracketed) && !withoutBracketed.Equals(cleaned, StringComparison.OrdinalIgnoreCase))
        {
            yield return withoutBracketed;
        }

        var words = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length > 1)
        {
            yield return words[^1];
        }
    }

    private static bool RangesOverlap(int firstStart, int firstLength, int secondStart, int secondLength)
    {
        var firstEnd = firstStart + firstLength;
        var secondEnd = secondStart + secondLength;
        return firstStart < secondEnd && secondStart < firstEnd;
    }

    private Brush CreateAnimatedVerseHighlightBrush(bool isSelected)
    {
        if (!isSelected)
        {
            return Brushes.Transparent;
        }

        var targetColor = GetResourceBrush("Mint") is SolidColorBrush mintBrush
            ? mintBrush.Color
            : Color.FromRgb(221, 161, 94);
        var brush = new SolidColorBrush(Color.FromArgb(0, targetColor.R, targetColor.G, targetColor.B));
        brush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(
            brush.Color,
            targetColor,
            TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        return brush;
    }

    private async void EndStudyNotification_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStudy is null)
        {
            return;
        }

        SaveWorkspaceState();

        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
            button.Content = "Sending...";
        }

        try
        {
            var apiResponse = await EndStudySessionThroughBackendAsync(_currentStudy);
            var sentCount = apiResponse.Notifications.Count(notification => notification.Sent);
            var failedCount = apiResponse.Notifications.Count - sentCount;
            var failureSummary = failedCount == 0
                ? string.Empty
                : $"\n\nFirst failure: {apiResponse.Notifications.First(notification => !notification.Sent).Error}";
            var overviewSummary = string.IsNullOrWhiteSpace(apiResponse.Overview)
                ? string.Empty
                : $"\n\nOverview: {apiResponse.Overview}";
            var deliverySummary = string.Join(
                "\n",
                apiResponse.Notifications.Select(notification =>
                    $"{notification.Title}: {notification.Delivery}"));
            MessageBox.Show(
                $"Notifications initiated.{overviewSummary}\n\nSent or scheduled: {sentCount}\nFailed: {failedCount}\n\nSchedule:\n{deliverySummary}{failureSummary}",
                "Notifications initiated",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            TrackScheduledNotifications(apiResponse.Notifications);
            SaveWorkspaceState();
            RenderScheduledNotifications();
            ShowToast("Notifications scheduled");
        }
        catch (HttpRequestException ex)
        {
            MessageBox.Show(
                $"The desktop app could not reach the local backend API.\n\n{FormatExceptionMessage(ex)}",
                "Backend connection failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (TaskCanceledException ex)
        {
            MessageBox.Show(
                $"The local backend API took too long to respond.\n\n{FormatExceptionMessage(ex)}",
                "Backend timeout",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"The notification could not be sent right now.\n\n{FormatExceptionMessage(ex)}",
                "Notification failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
        finally
        {
            if (button is not null)
            {
                button.IsEnabled = true;
                button.Content = "End study for now and initiate notifications";
            }
        }
    }

    private async void CancelScheduledNotifications_Click(object sender, RoutedEventArgs e)
    {
        var activeNotifications = _scheduledNotifications
            .Where(notification => !notification.Deleted && !string.IsNullOrWhiteSpace(notification.SequenceId))
            .ToList();

        if (activeNotifications.Count == 0)
        {
            RenderScheduledNotifications();
            ShowToast("No scheduled notifications to cancel");
            return;
        }

        var button = sender as Button;
        if (button is not null)
        {
            button.IsEnabled = false;
            button.Content = "Canceling...";
        }

        try
        {
            var sequenceIds = activeNotifications
                .Select(notification => notification.SequenceId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            var response = await CancelScheduledNotificationsThroughBackendAsync(sequenceIds);
            var canceledCount = response.Notifications.Count(notification => notification.Canceled);
            var failedCount = response.Notifications.Count - canceledCount;

            var canceledIds = response.Notifications
                .Where(notification => notification.Canceled)
                .Select(notification => notification.SequenceId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var notification in _scheduledNotifications.Where(notification => canceledIds.Contains(notification.SequenceId)))
            {
                notification.Deleted = true;
                notification.DeletedAt = DateTimeOffset.Now;
            }

            SaveWorkspaceState();
            RenderScheduledNotifications();

            ShowToast(failedCount == 0
                ? $"Canceled {canceledCount} scheduled notifications"
                : $"Canceled {canceledCount}; {failedCount} could not be canceled");
        }
        catch (Exception ex)
        {
            RenderScheduledNotifications();
            MessageBox.Show(
                $"Scheduled notifications could not be canceled right now.\n\n{FormatExceptionMessage(ex)}",
                "Cancel failed",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void TrackScheduledNotifications(List<NotificationDispatchApiResult> notifications)
    {
        foreach (var notification in notifications.Where(notification =>
                     notification.Sent
                     && !string.Equals(notification.Delivery, "now", StringComparison.OrdinalIgnoreCase)
                     && !string.IsNullOrWhiteSpace(notification.SequenceId)))
        {
            var existing = _scheduledNotifications.FirstOrDefault(candidate =>
                string.Equals(candidate.SequenceId, notification.SequenceId, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                existing.Title = notification.Title;
                existing.Delivery = notification.Delivery;
                existing.Deleted = false;
                existing.DeletedAt = null;
                continue;
            }

            _scheduledNotifications.Add(new ScheduledNotificationState
            {
                SequenceId = notification.SequenceId,
                Title = notification.Title,
                Delivery = notification.Delivery,
                CreatedAt = DateTimeOffset.Now,
                Deleted = false
            });
        }
    }

    private async Task<EndStudySessionApiResponse> EndStudySessionThroughBackendAsync(WorkspaceItem study)
    {
        await EnsureBackendRunningAsync();
        var reminders = CreateReminderApiRequests(study);

        var request = new EndStudySessionApiRequest(
            study.Name,
            CreateStudyReference(study),
            CreateSelectedScriptureText(study),
            CreateStudyNotesText(study),
            _selectedAiModel,
            OverviewNotificationEnabledCheckBox.IsChecked == true,
            NormalizeOverviewInstruction(OverviewPromptTextBox.Text),
            RemindersEnabledCheckBox.IsChecked == true,
            reminders);

        using var response = await BackendHttpClient.PostAsJsonAsync(
            $"{BackendApiUrl}/api/study-sessions/end",
            request);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {error}");
        }

        return await response.Content.ReadFromJsonAsync<EndStudySessionApiResponse>()
            ?? throw new InvalidOperationException("Backend returned an empty response.");
    }

    private async Task<CancelNotificationsApiResponse> CancelScheduledNotificationsThroughBackendAsync(
        List<string> sequenceIds)
    {
        await EnsureBackendRunningAsync();

        using var response = await BackendHttpClient.PostAsJsonAsync(
            $"{BackendApiUrl}/api/notifications/cancel",
            new CancelNotificationsApiRequest(sequenceIds));

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            throw new InvalidOperationException($"Backend returned {(int)response.StatusCode}: {error}");
        }

        return await response.Content.ReadFromJsonAsync<CancelNotificationsApiResponse>()
            ?? throw new InvalidOperationException("Backend returned an empty response.");
    }

    private void UpdateCancelScheduledNotificationsButton()
    {
        if (CancelScheduledNotificationsButton is null)
        {
            return;
        }

        var activeCount = _scheduledNotifications.Count(notification => !notification.Deleted);
        CancelScheduledNotificationsButton.IsEnabled = activeCount > 0;
        CancelScheduledNotificationsButton.Content = activeCount == 0
            ? "Cancel scheduled"
            : $"Cancel scheduled ({activeCount})";
    }

    private static string FormatExceptionMessage(Exception exception)
    {
        var messages = new List<string>();
        var cursor = exception;
        while (cursor is not null)
        {
            messages.Add(cursor.Message);
            cursor = cursor.InnerException;
        }

        return string.Join("\n", messages.Distinct());
    }

    private static async Task EnsureBackendRunningAsync()
    {
        if (await CanReachBackendAsync())
        {
            return;
        }

        var backendProjectPath = FindBackendProjectPath();
        if (backendProjectPath is null)
        {
            throw new InvalidOperationException("The backend API is not running, and the backend project could not be found.");
        }

        var startInfo = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments = $"run --project \"{backendProjectPath}\"",
            WorkingDirectory = Path.GetDirectoryName(backendProjectPath)!,
            CreateNoWindow = true,
            UseShellExecute = false
        };
        Process.Start(startInfo);

        for (var attempt = 0; attempt < 60; attempt++)
        {
            await Task.Delay(500);
            if (await CanReachBackendAsync())
            {
                return;
            }
        }

        throw new InvalidOperationException("The backend API did not start in time.");
    }

    private static async Task<bool> CanReachBackendAsync()
    {
        try
        {
            using var response = await BackendHttpClient.GetAsync($"{BackendApiUrl}/");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static string? FindBackendProjectPath()
    {
        var cursor = new DirectoryInfo(AppContext.BaseDirectory);
        while (cursor is not null)
        {
            var candidate = Path.Combine(cursor.FullName, "BibleStudy.Backend", "BibleStudy.Backend.csproj");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            cursor = cursor.Parent;
        }

        return null;
    }

    private string CreateStudyReference(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        return study.PassageLabel == "No passage selected"
            ? string.Empty
            : $"{root.Name} {study.PassageLabel}";
    }

    private static string CreateStudyNotesText(WorkspaceItem study)
    {
        var notes = new List<string>();
        foreach (var block in study.Blocks.Where(block => !string.IsNullOrWhiteSpace(block.Text)))
        {
            var text = block.Text.Trim();
            var formatted = block.Kind switch
            {
                EditorBlockKind.HeadingOne => $"# {text}",
                EditorBlockKind.HeadingTwo => $"## {text}",
                EditorBlockKind.HeadingThree => $"### {text}",
                EditorBlockKind.Quote => $"> {text}",
                EditorBlockKind.BulletedList => $"- {text}",
                EditorBlockKind.NumberedList => $"1. {text}",
                EditorBlockKind.Todo => $"- [{(block.IsChecked ? "x" : " ")}] {text}",
                EditorBlockKind.Callout => $"Important: {text}",
                EditorBlockKind.Code => $"Study detail: {text}",
                _ => text
            };
            notes.Add(formatted);
        }

        foreach (var notesPanel in study.ExtraPanels.Where(panel =>
                     panel.Kind == ExtraStudyPanelKind.Notes
                     && !string.IsNullOrWhiteSpace(panel.NotesText)))
        {
            var title = string.IsNullOrWhiteSpace(notesPanel.Title) ? "Notes panel" : notesPanel.Title.Trim();
            notes.Add($"## {title}\n{notesPanel.NotesText.Trim()}");
        }

        return string.Join("\n", notes);
    }

    private string CreateSelectedScriptureText(WorkspaceItem study)
    {
        var root = GetRootWorkspace(study);
        if (study.PassageStartChapter is null || study.PassageStartVerse is null)
        {
            return string.Empty;
        }

        var startChapter = study.PassageStartChapter.Value;
        var startVerse = study.PassageStartVerse.Value;
        var endChapter = study.PassageEndChapter ?? startChapter;
        var endVerse = study.PassageEndVerse ?? startVerse;

        if (endChapter < startChapter || (endChapter == startChapter && endVerse < startVerse))
        {
            (startChapter, endChapter) = (endChapter, startChapter);
            (startVerse, endVerse) = (endVerse, startVerse);
        }

        var selectedVerses = new List<string>();
        for (var chapter = startChapter; chapter <= endChapter; chapter++)
        {
            if (!TryGetChapterVerses(root.Name, chapter, out var verses))
            {
                continue;
            }

            var firstVerse = chapter == startChapter ? startVerse : 1;
            var lastVerse = chapter == endChapter ? endVerse : verses.Count;
            for (var verse = firstVerse; verse <= Math.Min(lastVerse, verses.Count); verse++)
            {
                var text = verses[verse - 1].Trim();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    selectedVerses.Add($"{chapter}:{verse} {text}");
                }
            }
        }

        return string.Join(" ", selectedVerses);
    }

    private List<StudyReminderApiRequest> CreateReminderApiRequests(WorkspaceItem study)
    {
        return _reminderScheduleItems
            .Where(item => !string.IsNullOrWhiteSpace(item.Prompt))
            .Select((item, index) => new StudyReminderApiRequest(
                $"reminder-{index + 1}",
                $"Study reminder {index + 1}",
                NormalizeReminderInstruction(item.Prompt),
                item.NtfyDelay))
            .ToList();
    }

    private void RefreshVerseSelectors(BibleBook? book, int? startChapter, int? endChapter)
    {
        StartVerseSelect.ItemsSource = GetVerseOptions(book, startChapter);
        EndVerseSelect.ItemsSource = GetVerseOptions(book, endChapter);
    }

    private static List<int> GetVerseOptions(BibleBook? book, int? chapter)
    {
        return Enumerable.Range(1, GetVerseCount(book, chapter)).ToList();
    }

    private static int? ClampVerseSelection(BibleBook? book, int? chapter, int? verse)
    {
        if (verse is null || chapter is null)
        {
            return null;
        }

        return Math.Clamp(verse.Value, 1, GetVerseCount(book, chapter));
    }

    private static int GetVerseCount(BibleBook? book, int? chapter)
    {
        if (book is null || chapter is null || chapter < 1 || chapter > book.VerseCounts.Length)
        {
            return 1;
        }

        return book.VerseCounts[chapter.Value - 1];
    }

    private static int? GetSelectedInt(ComboBox comboBox)
    {
        return comboBox.SelectedItem is int value
            ? value
            : null;
    }

    private void MarkCurrentStudyEdited()
    {
        if (_currentStudy is null)
        {
            return;
        }

        _currentStudy.LastEditedAt = DateTime.Now;
        var todayState = GetTodayState();
        var contextKey = CreateStudyContextKey(_currentStudy);
        todayState.ExcludedStudyContextKeys.RemoveAll(key =>
            string.Equals(key, contextKey, StringComparison.OrdinalIgnoreCase));
        RenderTodayDashboard();
        QueueWorkspaceSave();
    }

    private void RestoreContentShell()
    {
        ContentShell.Background = (Brush)FindResource("PanelBackground");
        ContentShell.BorderBrush = (Brush)FindResource("StrokeSoft");
        ContentShell.BorderThickness = new Thickness(1);
        ContentShell.CornerRadius = new CornerRadius(28);
        ContentShell.Padding = new Thickness(24);
    }

    private void FlattenContentShell()
    {
        ContentShell.Background = Brushes.Transparent;
        ContentShell.BorderBrush = Brushes.Transparent;
        ContentShell.BorderThickness = new Thickness(0);
        ContentShell.CornerRadius = new CornerRadius(0);
        ContentShell.Padding = new Thickness(0);
    }

    private void StudyBlock_KeyUp(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        if (_isApplyingEditorCommand)
        {
            return;
        }

        block.Text = textBox.Text;
        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;
        MarkCurrentStudyEdited();

        TryAutoCorrectPreviousWord(textBox, block, e.Key);

    }

    private void TryAutoCorrectPreviousWord(TextBox textBox, StudyBlock block, Key key)
    {
        if (textBox.SelectionLength > 0 || !IsWordBoundaryKey(key) || textBox.CaretIndex < 2)
        {
            return;
        }

        var checkIndex = Math.Max(0, textBox.CaretIndex - 2);
        var spellingError = textBox.GetSpellingError(checkIndex);
        if (spellingError is null)
        {
            return;
        }

        var suggestions = spellingError.Suggestions.Cast<string>().Take(2).ToList();
        if (suggestions.Count != 1)
        {
            return;
        }

        var start = textBox.GetSpellingErrorStart(checkIndex);
        var length = textBox.GetSpellingErrorLength(checkIndex);
        if (start < 0 || length <= 0)
        {
            return;
        }

        var suggestion = suggestions[0];
        var oldCaret = textBox.CaretIndex;
        _isApplyingEditorCommand = true;
        try
        {
            textBox.Text = textBox.Text.Remove(start, length).Insert(start, suggestion);
            textBox.CaretIndex = Math.Clamp(oldCaret + suggestion.Length - length, 0, textBox.Text.Length);
            block.Text = textBox.Text;
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }
    }

    private static bool IsWordBoundaryKey(Key key)
    {
        return key is Key.Space
            or Key.OemPeriod
            or Key.Decimal
            or Key.OemComma
            or Key.OemQuestion
            or Key.OemSemicolon
            or Key.OemQuotes;
    }

    private void StudyBlock_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;

        if (SlashCommandMenu.Visibility == Visibility.Visible && HandleSlashCommandKeyDown(e))
        {
            return;
        }

        if (e.Key == Key.Back && TryBackspaceAcrossStudyBlocks(textBox, block))
        {
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            return;
        }

        if (TryHandleInlineListEnter(textBox, block))
        {
            e.Handled = true;
            return;
        }

        InsertBlockAfter(textBox, block);
        e.Handled = true;
    }

    private void StudyBlock_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isApplyingEditorCommand || sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        block.Text = textBox.Text;
        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;
        MarkCurrentStudyEdited();
        QueueSmoothEditorCaretUpdate();

        if (textBox.Text.TrimStart().StartsWith("/", StringComparison.Ordinal))
        {
            _maybeInSlashCommand = true;
            UpdateSlashCommandMenu();
            return;
        }

        if (SlashCommandMenu.Visibility == Visibility.Visible || _maybeInSlashCommand)
        {
            HideSlashCommandMenu();
        }
    }

    private void StudyBlock_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (sender is not TextBox textBox || textBox.Tag is not StudyBlock block)
        {
            return;
        }

        _activeStudyBlock = block;
        _activeStudyTextBox = textBox;
        SelectableText_SelectionChanged(textBox, e);
        QueueSmoothEditorCaretUpdate();
    }

    private void StudyBlock_CheckChanged(object sender, RoutedEventArgs e)
    {
        MarkCurrentStudyEdited();
    }

    private void StudyBlock_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.Tag is StudyBlock block)
        {
            if (!ReferenceEquals(_activeStudyTextBox, textBox))
            {
                HideSlashCommandMenu();
            }

            if (_activeStudyBlock is not null && !ReferenceEquals(_activeStudyBlock, block))
            {
                _activeStudyBlock.IsFocused = false;
            }

            _activeStudyBlock = block;
            _activeStudyTextBox = textBox;
            block.IsFocused = true;
            QueueSmoothEditorCaretUpdate(animate: false);
        }
    }

    private void StudyBlock_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (sender is TextBox { Tag: StudyBlock block })
        {
            block.IsFocused = false;
        }

        if (ReferenceEquals(sender, _activeStudyTextBox))
        {
            SmoothEditorCaret.Visibility = Visibility.Collapsed;
        }
    }

    private void QueueSmoothEditorCaretUpdate(bool animate = true)
    {
        Dispatcher.BeginInvoke(() => UpdateSmoothEditorCaret(animate), DispatcherPriority.Render);
    }

    private void UpdateSmoothEditorCaret(bool animate)
    {
        if (_activeStudyTextBox is null
            || !_activeStudyTextBox.IsKeyboardFocusWithin
            || StudyEditorCaretLayer is null
            || SmoothEditorCaret is null)
        {
            if (SmoothEditorCaret is not null)
            {
                SmoothEditorCaret.Visibility = Visibility.Collapsed;
            }

            return;
        }

        var caretIndex = Math.Clamp(_activeStudyTextBox.CaretIndex, 0, _activeStudyTextBox.Text.Length);
        var caretRect = _activeStudyTextBox.GetRectFromCharacterIndex(caretIndex, trailingEdge: true);
        if (caretRect.IsEmpty)
        {
            caretRect = _activeStudyTextBox.GetRectFromCharacterIndex(caretIndex, trailingEdge: false);
        }

        if (caretRect.IsEmpty)
        {
            SmoothEditorCaret.Visibility = Visibility.Collapsed;
            return;
        }

        var target = _activeStudyTextBox.TranslatePoint(new Point(caretRect.X, caretRect.Y), StudyEditorCaretLayer);
        var caretHeight = Math.Max(18, caretRect.Height > 0 ? caretRect.Height : _activeStudyTextBox.FontSize * 1.35);
        SmoothEditorCaret.Height = caretHeight;
        SmoothEditorCaret.Visibility = Visibility.Visible;

        var currentLeft = Canvas.GetLeft(SmoothEditorCaret);
        var currentTop = Canvas.GetTop(SmoothEditorCaret);
        if (!animate || double.IsNaN(currentLeft) || double.IsNaN(currentTop))
        {
            SmoothEditorCaret.BeginAnimation(Canvas.LeftProperty, null);
            SmoothEditorCaret.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(SmoothEditorCaret, target.X);
            Canvas.SetTop(SmoothEditorCaret, target.Y);
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(90);
        SmoothEditorCaret.BeginAnimation(Canvas.LeftProperty, new DoubleAnimation(target.X, duration)
        {
            EasingFunction = ease
        });
        SmoothEditorCaret.BeginAnimation(Canvas.TopProperty, new DoubleAnimation(target.Y, duration)
        {
            EasingFunction = ease
        });
    }

    private bool HandleSlashCommandKeyDown(KeyEventArgs e)
    {
        if (SlashCommandMenu.Visibility != Visibility.Visible)
        {
            return false;
        }

        if (e.Key == Key.Escape)
        {
            HideSlashCommandMenu();
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Down)
        {
            MoveSlashCommandSelection(1);
            e.Handled = true;
            return true;
        }

        if (e.Key == Key.Up)
        {
            MoveSlashCommandSelection(-1);
            e.Handled = true;
            return true;
        }

        if (e.Key is Key.Enter or Key.Tab)
        {
            if (_activeStudyTextBox is not null && _activeStudyBlock is not null)
            {
                _activeStudyBlock.Text = _activeStudyTextBox.Text;
            }

            UpdateSlashCommandMenu();
            if (_filteredSlashCommands.Count > 0)
            {
                ApplySelectedSlashCommand();
            }

            e.Handled = true;
            return true;
        }

        return false;
    }

    private void SlashCommandRefreshTimer_Tick(object? sender, EventArgs e)
    {
        _slashCommandRefreshTimer.Stop();
        UpdateSlashCommandMenu();
    }

    private void SlashCommandList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindParent<ListBoxItem>((DependencyObject)e.OriginalSource) is not { DataContext: EditorCommandOption option })
        {
            return;
        }

        SlashCommandList.SelectedItem = option;
        ApplySelectedSlashCommand();
    }

    private void UpdateSlashCommandMenu()
    {
        var query = GetSlashCommandQuery();
        if (query is null)
        {
            HideSlashCommandMenu();
            return;
        }

        if (query == _lastSlashQuery && SlashCommandMenu.Visibility == Visibility.Visible)
        {
            PositionSlashCommandMenu();
            return;
        }

        _lastSlashQuery = query;
        var matches = _slashCommands
            .Where(command => command.Matches(query))
            .ToList();

        _filteredSlashCommands.Clear();
        foreach (var match in matches)
        {
            _filteredSlashCommands.Add(match);
        }

        if (_filteredSlashCommands.Count == 0)
        {
            HideSlashCommandMenu();
            return;
        }

        SlashCommandList.SelectedIndex = 0;
        ShowSlashCommandMenu();
    }

    private void HideSlashCommandMenu()
    {
        _slashCommandRefreshTimer.Stop();
        if (SlashCommandMenu is not null && SlashCommandMenu.Visibility == Visibility.Visible)
        {
            var fade = new DoubleAnimation(SlashCommandMenu.Opacity, 0, TimeSpan.FromMilliseconds(90))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) => SlashCommandMenu.Visibility = Visibility.Collapsed;
            SlashCommandMenu.BeginAnimation(OpacityProperty, fade);
            SlashCommandMenuScale?.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, 0.96, TimeSpan.FromMilliseconds(90)));
            SlashCommandMenuScale?.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, 0.96, TimeSpan.FromMilliseconds(90)));
        }
        _lastSlashQuery = string.Empty;
        _maybeInSlashCommand = false;
        _filteredSlashCommands.Clear();
    }

    private void ShowSlashCommandMenu()
    {
        if (SlashCommandMenu is null || SlashCommandMenuScale is null)
        {
            return;
        }

        PositionSlashCommandMenu();
        var wasVisible = SlashCommandMenu.Visibility == Visibility.Visible;
        SlashCommandMenu.Visibility = Visibility.Visible;
        if (wasVisible)
        {
            SlashCommandMenu.Opacity = 1;
            return;
        }

        SlashCommandMenu.Opacity = 0;
        SlashCommandMenuScale.ScaleX = 0.96;
        SlashCommandMenuScale.ScaleY = 0.96;
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        SlashCommandMenu.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = ease
        });
        SlashCommandMenuScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
        SlashCommandMenuScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
    }

    private void PositionSlashCommandMenu()
    {
        if (_activeStudyTextBox is null || SlashCommandLayer is null || SlashCommandMenu is null)
        {
            return;
        }

        var caretIndex = Math.Clamp(_activeStudyTextBox.CaretIndex, 0, _activeStudyTextBox.Text.Length);
        var caretRect = _activeStudyTextBox.GetRectFromCharacterIndex(caretIndex, trailingEdge: true);
        if (caretRect.IsEmpty)
        {
            caretRect = _activeStudyTextBox.GetRectFromCharacterIndex(caretIndex, trailingEdge: false);
        }

        if (caretRect.IsEmpty)
        {
            return;
        }

        var caretBottom = _activeStudyTextBox.TranslatePoint(new Point(caretRect.X, caretRect.Bottom), SlashCommandLayer);
        var targetLeft = caretBottom.X;
        var targetTop = caretBottom.Y + 8;
        var layerWidth = Math.Max(1, SlashCommandLayer.ActualWidth);
        var layerHeight = Math.Max(1, SlashCommandLayer.ActualHeight);
        var menuWidth = SlashCommandMenu.ActualWidth > 0 ? SlashCommandMenu.ActualWidth : SlashCommandMenu.Width;
        var menuHeight = SlashCommandMenu.ActualHeight > 0 ? SlashCommandMenu.ActualHeight : 240;

        if (targetTop + menuHeight > layerHeight - 8)
        {
            targetTop = _activeStudyTextBox.TranslatePoint(new Point(caretRect.X, caretRect.Top), SlashCommandLayer).Y - menuHeight - 8;
        }

        Canvas.SetLeft(SlashCommandMenu, Math.Round(Math.Clamp(targetLeft, 8, Math.Max(8, layerWidth - menuWidth - 8))));
        Canvas.SetTop(SlashCommandMenu, Math.Round(Math.Clamp(targetTop, 8, Math.Max(8, layerHeight - menuHeight - 8))));
    }

    private void MoveSlashCommandSelection(int direction)
    {
        if (_filteredSlashCommands.Count == 0)
        {
            return;
        }

        var selectedIndex = SlashCommandList.SelectedIndex < 0
            ? 0
            : SlashCommandList.SelectedIndex;
        selectedIndex = (selectedIndex + direction + _filteredSlashCommands.Count) % _filteredSlashCommands.Count;
        SlashCommandList.SelectedIndex = selectedIndex;
        SlashCommandList.ScrollIntoView(_filteredSlashCommands[selectedIndex]);
    }

    private void ApplySelectedSlashCommand()
    {
        if (SlashCommandList.SelectedItem is EditorCommandOption option)
        {
            ApplyBlockCommand(option.Kind);
        }
    }

    private void ApplyBlockCommand(EditorBlockKind blockKind)
    {
        if (_activeStudyBlock is null || _activeStudyTextBox is null)
        {
            return;
        }

        _isApplyingEditorCommand = true;
        try
        {
            var startingText = GetInitialTextForBlock(blockKind);
            _activeStudyBlock.Text = startingText;
            _activeStudyBlock.Kind = blockKind;
            _activeStudyBlock.IsChecked = false;
            _activeStudyTextBox.Text = startingText;
            _activeStudyTextBox.CaretIndex = startingText.Length;
            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        _activeStudyTextBox.Focus();
    }

    private void InsertBlockAfter(TextBox textBox, StudyBlock block)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var selectionStart = Math.Min(textBox.SelectionStart, textBox.Text.Length);
        var selectionEnd = Math.Min(selectionStart + textBox.SelectionLength, textBox.Text.Length);
        var currentText = textBox.Text[..selectionStart];
        var nextText = textBox.Text[selectionEnd..];
        if (IsContinuableListBlock(block.Kind) && string.IsNullOrWhiteSpace(textBox.Text))
        {
            _isApplyingEditorCommand = true;
            try
            {
                block.Kind = EditorBlockKind.Normal;
                block.Text = string.Empty;
                block.IsChecked = false;
                textBox.Text = string.Empty;
                textBox.CaretIndex = 0;
                UpdateNumberedListLabels();
                MarkCurrentStudyEdited();
            }
            finally
            {
                _isApplyingEditorCommand = false;
            }

            HideSlashCommandMenu();
            FocusBlock(block, 0);
            return;
        }

        var nextBlock = CreateStudyBlock(IsContinuableListBlock(block.Kind)
            ? block.Kind
            : EditorBlockKind.Normal);
        nextBlock.Text = nextText;

        _isApplyingEditorCommand = true;
        try
        {
            block.Text = currentText;
            textBox.Text = currentText;
            textBox.CaretIndex = currentText.Length;

            var blockIndex = _currentStudy.Blocks.IndexOf(block);
            if (blockIndex < 0)
            {
                _currentStudy.Blocks.Add(nextBlock);
            }
            else
            {
                _currentStudy.Blocks.Insert(blockIndex + 1, nextBlock);
            }

            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        FocusBlock(nextBlock, 0);
    }

    private static bool IsContinuableListBlock(EditorBlockKind kind)
    {
        return kind is EditorBlockKind.BulletedList or EditorBlockKind.NumberedList or EditorBlockKind.Todo;
    }

    private bool TryHandleInlineListEnter(TextBox textBox, StudyBlock block)
    {
        if (_currentStudy is null || !IsInlineListBlock(block.Kind))
        {
            return false;
        }

        var text = textBox.Text;
        var selectionStart = Math.Min(textBox.SelectionStart, text.Length);
        var selectionEnd = Math.Min(selectionStart + textBox.SelectionLength, text.Length);
        if (selectionEnd > selectionStart)
        {
            text = text.Remove(selectionStart, selectionEnd - selectionStart);
            textBox.Text = text;
            textBox.CaretIndex = selectionStart;
        }

        var lineStart = text.LastIndexOf('\n', Math.Max(0, selectionStart - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = text.IndexOf('\n', selectionStart);
        lineEnd = lineEnd < 0 ? text.Length : lineEnd;
        var currentLine = text[lineStart..lineEnd].TrimEnd('\r');
        var currentItemText = StripInlineListPrefix(currentLine, block.Kind).Trim();

        if (string.IsNullOrWhiteSpace(currentItemText))
        {
            ExitInlineListAtCurrentLine(textBox, block, lineStart, lineEnd);
            return true;
        }

        var nextPrefix = GetNextInlineListPrefix(text, selectionStart, block.Kind);
        var insertion = Environment.NewLine + nextPrefix;
        textBox.Text = text.Insert(selectionStart, insertion);
        textBox.CaretIndex = selectionStart + insertion.Length;
        block.Text = textBox.Text;
        MarkCurrentStudyEdited();
        HideSlashCommandMenu();
        return true;
    }

    private void ExitInlineListAtCurrentLine(TextBox textBox, StudyBlock block, int lineStart, int lineEnd)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var text = textBox.Text;
        var removeStart = lineStart > 0 ? lineStart - 1 : lineStart;
        var removeEnd = lineEnd;
        if (removeEnd < text.Length && text[removeEnd] == '\r')
        {
            removeEnd++;
        }

        if (removeEnd < text.Length && text[removeEnd] == '\n')
        {
            removeEnd++;
        }

        var remainingText = text.Remove(removeStart, removeEnd - removeStart).TrimEnd('\r', '\n');
        if (string.IsNullOrWhiteSpace(remainingText))
        {
            _isApplyingEditorCommand = true;
            try
            {
                block.Kind = EditorBlockKind.Normal;
                block.Text = string.Empty;
                textBox.Text = string.Empty;
                textBox.CaretIndex = 0;
                MarkCurrentStudyEdited();
            }
            finally
            {
                _isApplyingEditorCommand = false;
            }

            HideSlashCommandMenu();
            FocusBlock(block, 0);
            return;
        }

        var nextBlock = CreateNormalStudyBlock();
        var blockIndex = _currentStudy.Blocks.IndexOf(block);
        _isApplyingEditorCommand = true;
        try
        {
            block.Text = remainingText;
            textBox.Text = remainingText;
            textBox.CaretIndex = textBox.Text.Length;
            if (blockIndex < 0)
            {
                _currentStudy.Blocks.Add(nextBlock);
            }
            else
            {
                _currentStudy.Blocks.Insert(blockIndex + 1, nextBlock);
            }

            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        FocusBlock(nextBlock, 0);
    }

    private static bool IsInlineListBlock(EditorBlockKind kind)
    {
        return kind is EditorBlockKind.BulletedList or EditorBlockKind.NumberedList;
    }

    private static string StripInlineListPrefix(string line, EditorBlockKind kind)
    {
        if (kind == EditorBlockKind.BulletedList && line.TrimStart().StartsWith("\u2022", StringComparison.Ordinal))
        {
            var bulletIndex = line.IndexOf('\u2022');
            return line[(bulletIndex + 1)..].TrimStart();
        }

        if (kind == EditorBlockKind.NumberedList)
        {
            var trimmed = line.TrimStart();
            var dotIndex = trimmed.IndexOf('.');
            if (dotIndex > 0 && trimmed[..dotIndex].All(char.IsDigit))
            {
                return trimmed[(dotIndex + 1)..].TrimStart();
            }
        }

        return line;
    }

    private static string GetNextInlineListPrefix(string text, int insertionIndex, EditorBlockKind kind)
    {
        if (kind == EditorBlockKind.BulletedList)
        {
            return "\u2022 ";
        }

        var linesBeforeCaret = text[..Math.Min(insertionIndex, text.Length)]
            .Split('\n')
            .Count(line => line.TrimStart().TakeWhile(char.IsDigit).Any());
        return $"{Math.Max(1, linesBeforeCaret + 1)}. ";
    }

    private bool TryBackspaceAcrossStudyBlocks(TextBox textBox, StudyBlock block)
    {
        if (_currentStudy is null
            || textBox.SelectionLength > 0
            || textBox.CaretIndex != 0)
        {
            return false;
        }

        var blockIndex = _currentStudy.Blocks.IndexOf(block);
        if (blockIndex <= 0)
        {
            return false;
        }

        var previousBlock = _currentStudy.Blocks[blockIndex - 1];
        var previousCaretIndex = previousBlock.Text.Length;
        var currentText = textBox.Text;

        _isApplyingEditorCommand = true;
        try
        {
            if (!string.IsNullOrEmpty(currentText))
            {
                previousBlock.Text += currentText;
            }

            _currentStudy.Blocks.RemoveAt(blockIndex);
            UpdateNumberedListLabels();
            MarkCurrentStudyEdited();
        }
        finally
        {
            _isApplyingEditorCommand = false;
        }

        HideSlashCommandMenu();
        FocusBlock(previousBlock, previousCaretIndex);
        return true;
    }

    private void QueueSlashCommandRefresh()
    {
        _slashCommandRefreshTimer.Stop();
        _slashCommandRefreshTimer.Start();
    }

    private static string GetInitialTextForBlock(EditorBlockKind blockKind)
    {
        return blockKind switch
        {
            EditorBlockKind.BulletedList => "\u2022 ",
            EditorBlockKind.NumberedList => "1. ",
            _ => string.Empty
        };
    }

    private string? GetSlashCommandQuery()
    {
        var paragraphText = _activeStudyTextBox?.Text.TrimStart() ?? string.Empty;
        if (!paragraphText.StartsWith("/", StringComparison.Ordinal))
        {
            return null;
        }

        var query = paragraphText[1..].Trim();
        return query.Contains(' ', StringComparison.Ordinal)
            ? null
            : query;
    }

    private void EnsureStudyHasBlocks(WorkspaceItem study)
    {
        if (study.Blocks.Count == 0)
        {
            study.Blocks.Add(CreateNormalStudyBlock());
        }
    }

    private StudyBlock CreateNormalStudyBlock()
    {
        return CreateStudyBlock(EditorBlockKind.Normal);
    }

    private StudyBlock CreateStudyBlock(EditorBlockKind kind)
    {
        return new StudyBlock(
            kind,
            PickEditorBrush("TextPrimary"),
            PickEditorBrush("TextSecondary"),
            PickEditorBrush("Mint"),
            PickEditorBrush("PanelBackground"),
            PickEditorBrush("AppBackground"),
            PickEditorBrush("Coral"));
    }

    private void RefreshStudyBlockThemeBrushes()
    {
        foreach (var study in GetAllStudies())
        {
            foreach (var block in study.Blocks)
            {
                block.UpdateThemeBrushes(
                    PickEditorBrush("TextPrimary"),
                    PickEditorBrush("TextSecondary"),
                    PickEditorBrush("Mint"),
                    PickEditorBrush("PanelBackground"),
                    PickEditorBrush("AppBackground"),
                    PickEditorBrush("Coral"));
            }
        }
    }

    private void UpdateNumberedListLabels()
    {
        if (_currentStudy is null)
        {
            return;
        }

        var listIndex = 1;
        foreach (var block in _currentStudy.Blocks)
        {
            if (block.Kind == EditorBlockKind.NumberedList)
            {
                block.NumberLabel = $"{listIndex}.";
                listIndex++;
                continue;
            }

            listIndex = 1;
        }
    }

    private void FocusBlock(StudyBlock block, int? caretIndex = null)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var container = StudyBlockItems.ItemContainerGenerator.ContainerFromItem(block) as DependencyObject;
            var textBox = container is null ? null : FindChild<TextBox>(container);
            textBox?.Focus();
            if (textBox is not null)
            {
                textBox.CaretIndex = Math.Clamp(caretIndex ?? textBox.Text.Length, 0, textBox.Text.Length);
            }
        }, DispatcherPriority.Background);
    }

    private Brush PickEditorBrush(string resourceName)
    {
        return GetResourceBrush(resourceName);
    }

    private Brush GetResourceBrush(string resourceName)
    {
        return TryFindResource(resourceName) as Brush
            ?? new SolidColorBrush(Color.FromRgb(254, 250, 224));
    }

    private static List<WorkspaceItem> GetContainerLineage(WorkspaceItem item)
    {
        var lineage = new List<WorkspaceItem>();
        var cursor = item;

        while (cursor is not null)
        {
            lineage.Add(cursor);
            cursor = cursor.Parent;
        }

        lineage.Reverse();
        return lineage;
    }

    private void SidebarToggle_Click(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        var targetWidth = _sidebarCollapsed ? 64 : 284;
        var targetPadding = _sidebarCollapsed ? new Thickness(8, 22, 8, 22) : new Thickness(22);
        AnimateSidebarWidth(SidebarColumn.ActualWidth > 0 ? SidebarColumn.ActualWidth : SidebarColumn.Width.Value, targetWidth);
        SidebarRoot.BeginAnimation(Border.PaddingProperty, new ThicknessAnimation(SidebarRoot.Padding, targetPadding, TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        });
        SidebarToggleButton.HorizontalAlignment = _sidebarCollapsed ? HorizontalAlignment.Center : HorizontalAlignment.Right;
        SidebarToggleButton.ToolTip = _sidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";

        var menuVisibility = _sidebarCollapsed ? Visibility.Collapsed : Visibility.Visible;
        BibleStudyNavButton.Visibility = menuVisibility;
        TodayNavButton.Visibility = menuVisibility;
        ScriptureMemoryNavButton.Visibility = menuVisibility;
        SettingsNavButton.Visibility = menuVisibility;
        PrayerBoardNavButton.Visibility = menuVisibility;
        ReadingPlanNavButton.Visibility = menuVisibility;
        ResourceLibraryNavButton.Visibility = menuVisibility;
        NavTabIndicator.Visibility = Visibility.Collapsed;
    }

    private void AnimateSidebarWidth(double fromWidth, double toWidth)
    {
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        SidebarColumn.Width = new GridLength(Math.Max(0, fromWidth));
        var animation = new GridLengthAnimation
        {
            From = new GridLength(Math.Max(0, fromWidth)),
            To = new GridLength(Math.Max(0, toWidth)),
            Duration = new Duration(TimeSpan.FromMilliseconds(240)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseInOut }
        };
        animation.Completed += (_, _) =>
        {
            SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            SidebarColumn.Width = new GridLength(toWidth);
            if (!_sidebarCollapsed)
            {
                Dispatcher.BeginInvoke(() =>
                {
                    NavTabIndicator.Visibility = Visibility.Visible;
                    PositionNavTabIndicator(GetActiveNavButton(_activeNavTab));
                }, DispatcherPriority.ContextIdle);
            }
        };
        SidebarColumn.BeginAnimation(ColumnDefinition.WidthProperty, animation);
    }

    private void ScripturePanelToggle_Click(object sender, RoutedEventArgs e)
    {
        _scripturePanelHidden = !_scripturePanelHidden;

        if (_scripturePanelHidden)
        {
            if (ScriptureColumn.ActualWidth > 0)
            {
                _scripturePanelVisibleWidth = new GridLength(ScriptureColumn.ActualWidth);
            }

            StopScriptureSmoothScroll();
            HideStrongsPanel(animate: true);
            ScriptureColumn.MinWidth = 0;
            ScripturePanelRoot.BeginAnimation(OpacityProperty, null);
            AnimateScripturePanelCollapseThenClose(ScriptureColumn.ActualWidth);
            ScripturePanelToggleButton.ToolTip = "Show scripture panel";
            ScripturePanelToggleButtonText.Text = "X";
            ScripturePanelShowButtonText.Text = "Show";
            return;
        }

        var targetWidth = _scripturePanelVisibleWidth.Value <= 0
            ? 440
            : Math.Clamp(_scripturePanelVisibleWidth.Value, 1, Math.Max(1, StudyPanel.ActualWidth));
        ScriptureColumn.MinWidth = 0;
        ScriptureColumnSplitter.Visibility = Visibility.Collapsed;
        ScripturePanelRoot.Visibility = Visibility.Collapsed;
        ScripturePanelRoot.Opacity = 1;
        ScripturePanelScale.ScaleX = 0.04;
        ScripturePanelScale.ScaleY = 0.04;
        ScripturePanelRotate.Angle = -5;
        ScripturePanelShowButton.Visibility = Visibility.Collapsed;
        AnimateScripturePanelWidth(ScriptureColumn.ActualWidth, targetWidth, hiding: false);
        Dispatcher.BeginInvoke(() =>
        {
            if (_currentStudy is not null)
            {
                ApplySavedPanelGeometry(MovablePanelKind.Scripture, _currentStudy.ScripturePanelGeometry);
                ClampAllPanelsToStudyArea(save: false);
            }
        }, DispatcherPriority.Loaded);
        ScripturePanelToggleButton.ToolTip = "Hide scripture panel";
        ScripturePanelToggleButtonText.Text = "X";
        ScripturePanelShowButtonText.Text = "Show";

        if (_currentStudy is not null)
        {
            RenderScripturePanel(_currentStudy, _scriptureVisibleChapter, scrollToSelection: false);
        }
    }

    private void StrongsToggle_Changed(object sender, RoutedEventArgs e)
    {
        _strongsEnabled = StrongsToggleButton.IsChecked == true;
        if (!_strongsEnabled)
        {
            HideStrongsPanel(animate: true);
        }
        else
        {
            HideStrongsHoverPreview();
            ScriptureSelectionRail.Visibility = Visibility.Collapsed;
            ScriptureSelectionMarker.Visibility = Visibility.Collapsed;
        }

        RenderScriptureDocument();
        if (!_strongsEnabled)
        {
            UpdateScriptureSelectionMarker();
        }
    }

    private void StrongsPhrase_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Span { Tag: StrongsSelection selection })
        {
            return;
        }

        e.Handled = true;
        ShowStrongsPanel(selection);
    }

    private void StrongsLink_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Hyperlink { Tag: StrongsSelection selection })
        {
            return;
        }

        ShowStrongsHoverPreview(selection);
    }

    private void StrongsLink_MouseLeave(object sender, MouseEventArgs e)
    {
        HideStrongsHoverPreview();
    }

    private void CloseStrongsPanel_Click(object sender, RoutedEventArgs e)
    {
        HideStrongsPanel(animate: true);
    }

    private void ShowStrongsHoverPreview(StrongsSelection selection)
    {
        var entry = selection.Entry;
        var greekText = !string.IsNullOrWhiteSpace(entry.Greek)
            ? entry.Greek
            : selection.Phrase;

        StrongsHoverGreekText.Text = greekText;
        StrongsHoverStrongText.Text = string.IsNullOrWhiteSpace(entry.DStrong)
            ? string.Empty
            : entry.DStrong;
        StrongsHoverGlossText.Text = FirstNonEmpty(entry.Gloss, entry.SubMeaning, entry.DictionaryForm);
        StrongsHoverTranslationText.Text = string.IsNullOrWhiteSpace(entry.English)
            ? selection.Phrase
            : $"Translation: {entry.English}";

        StrongsHoverPopup.IsOpen = true;
        StrongsHoverPopupRoot.BeginAnimation(OpacityProperty, null);
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        StrongsHoverPopupRoot.Opacity = 0;
        StrongsHoverPopupScale.ScaleX = 0.72;
        StrongsHoverPopupScale.ScaleY = 0.72;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        StrongsHoverPopupRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
        {
            EasingFunction = ease
        });
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.72, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.72, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
    }

    private void HideStrongsHoverPreview()
    {
        if (!StrongsHoverPopup.IsOpen)
        {
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(StrongsHoverPopupRoot.Opacity, 0, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        };
        fade.Completed += (_, _) => StrongsHoverPopup.IsOpen = false;
        StrongsHoverPopupRoot.BeginAnimation(OpacityProperty, fade);
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(StrongsHoverPopupScale.ScaleX, 0.86, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        });
        StrongsHoverPopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(StrongsHoverPopupScale.ScaleY, 0.86, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        });
    }

    private void VerseReference_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Span { Tag: VerseReference reference })
        {
            ShowVerseReferencePreview(reference);
        }
    }

    private void VerseReference_MouseLeave(object sender, MouseEventArgs e)
    {
        HideVerseReferencePreview();
    }

    private void VerseReference_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Span { Tag: VerseReference reference })
        {
            return;
        }

        RenderScriptureReferencePanel(reference);
        e.Handled = true;
    }

    private void ShowVerseReferencePreview(VerseReference reference)
    {
        VerseReferencePopupTitle.Text = $"{reference.BookName} {reference.Chapter}:{reference.Verse}";
        VerseReferencePopupText.Text = TryGetVerseText(reference, out var verseText)
            ? verseText
            : "This verse is not available in the current scripture data.";

        VerseReferencePopup.IsOpen = true;
        VerseReferencePopupRoot.BeginAnimation(OpacityProperty, null);
        VerseReferencePopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        VerseReferencePopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        VerseReferencePopupRoot.Opacity = 0;
        VerseReferencePopupScale.ScaleX = 0.88;
        VerseReferencePopupScale.ScaleY = 0.88;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        VerseReferencePopupRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(130))
        {
            EasingFunction = ease
        });
        VerseReferencePopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
        VerseReferencePopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.88, 1, TimeSpan.FromMilliseconds(170))
        {
            EasingFunction = ease
        });
    }

    private void HideVerseReferencePreview()
    {
        if (!VerseReferencePopup.IsOpen)
        {
            return;
        }

        var ease = new CubicEase { EasingMode = EasingMode.EaseIn };
        var fade = new DoubleAnimation(VerseReferencePopupRoot.Opacity, 0, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        };
        fade.Completed += (_, _) => VerseReferencePopup.IsOpen = false;
        VerseReferencePopupRoot.BeginAnimation(OpacityProperty, fade);
        VerseReferencePopupScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(VerseReferencePopupScale.ScaleX, 0.92, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        });
        VerseReferencePopupScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(VerseReferencePopupScale.ScaleY, 0.92, TimeSpan.FromMilliseconds(90))
        {
            EasingFunction = ease
        });
    }

    private bool TryGetVerseText(VerseReference reference, out string verseText)
    {
        verseText = string.Empty;
        if (!TryGetChapterVerses(reference.BookName, reference.Chapter, out var verses)
            || reference.Verse < 1
            || reference.Verse > verses.Count)
        {
            return false;
        }

        verseText = verses[reference.Verse - 1];
        return !string.IsNullOrWhiteSpace(verseText);
    }

    private void RenderScriptureReferencePanel(VerseReference reference)
    {
        if (!TryGetChapterVerses(reference.BookName, reference.Chapter, out _))
        {
            ShowVerseReferencePreview(reference);
            return;
        }

        AddExtraScripturePanel(reference.BookName, reference.Chapter, reference.Verse, $"Opened from {reference.DisplayText}");
    }

    private void AddScripturePanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        var bookName = _scriptureVisibleBookName
                       ?? _selectedBook?.Name
                       ?? BibleBooks.FirstOrDefault()?.Name
                       ?? "Genesis";
        var chapter = _scriptureVisibleChapter
                      ?? _currentStudy?.PassageStartChapter
                      ?? 1;
        AddExtraScripturePanel(bookName, chapter, _currentStudy?.PassageStartVerse, "Added panel");
    }

    private void AddNotesPanelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        AddExtraNotesPanel();
    }

    private void StudyBlocks_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var originalSource = e.OriginalSource as DependencyObject;
        if (IsDescendantOf(originalSource, SelectionActionMenuRoot)
            || IsDescendantOf(originalSource, HighlightColorCard)
            || IsDescendantOf(originalSource, AnnotationNoteCard)
            || IsDescendantOf(originalSource, SavedNoteCard))
        {
            return;
        }

        if (SelectionActionPopup.IsOpen || HighlightColorPopup.IsOpen || AnnotationNotePopup.IsOpen)
        {
            CloseSelectionUi();
        }
        ClearMultiBlockSelectionPreview();
        if (e.ChangedButton != MouseButton.Left || FindAncestor<TextBox>(e.OriginalSource as DependencyObject) is not { Tag: StudyBlock } textBox)
        {
            _multiSelectStartTextBox = null;
            return;
        }

        _multiSelectStartTextBox = textBox;
        _multiSelectEndTextBox = textBox;
        _isMultiBlockDragging = false;
        _multiSelectStartIndex = textBox.GetCharacterIndexFromPoint(e.GetPosition(textBox), true);
        _multiSelectEndIndex = _multiSelectStartIndex;
    }

    private void StudyBlocks_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_multiSelectStartTextBox is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pointer = e.GetPosition(StudyBlockItems);
        var candidates = FindDescendants<TextBox>(StudyBlockItems)
            .Where(box => box.Tag is StudyBlock && box.IsVisible)
            .Select(textBox => new
            {
                TextBox = textBox,
                Origin = textBox.TranslatePoint(new Point(), StudyBlockItems)
            })
            .ToList();
        var target = candidates.FirstOrDefault(candidate =>
                new Rect(candidate.Origin, candidate.TextBox.RenderSize).Contains(pointer))
            ?? candidates.OrderBy(candidate => Math.Abs(pointer.Y - (candidate.Origin.Y + candidate.TextBox.ActualHeight / 2))).FirstOrDefault();
        if (target is not null)
        {
            _multiSelectEndTextBox = target.TextBox;
            var localPoint = e.GetPosition(target.TextBox);
            localPoint.X = Math.Clamp(localPoint.X, 0, Math.Max(0, target.TextBox.ActualWidth - 1));
            localPoint.Y = Math.Clamp(localPoint.Y, 0, Math.Max(0, target.TextBox.ActualHeight - 1));
            _multiSelectEndIndex = target.TextBox.GetCharacterIndexFromPoint(localPoint, true);
            if (!ReferenceEquals(_multiSelectStartTextBox, _multiSelectEndTextBox) && !_isMultiBlockDragging)
            {
                _isMultiBlockDragging = true;
                Mouse.Capture(this, CaptureMode.SubTree);
                SmoothEditorCaret.Visibility = Visibility.Collapsed;
            }

            if (_isMultiBlockDragging)
            {
                UpdateMultiBlockSelectionPreview();
                e.Handled = true;
            }
        }
    }

    private void StudyBlocks_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_multiSelectStartTextBox is null || _multiSelectEndTextBox is null
            || ReferenceEquals(_multiSelectStartTextBox, _multiSelectEndTextBox))
        {
            ClearMultiBlockSelectionPreview();
            if (_isMultiBlockDragging) Mouse.Capture(null);
            _isMultiBlockDragging = false;
            _multiSelectStartTextBox = null;
            return;
        }

        var startBox = _multiSelectStartTextBox;
        var endBox = _multiSelectEndTextBox;
        var startIndex = _multiSelectStartIndex;
        var endIndex = _multiSelectEndIndex;
        _multiSelectStartTextBox = null;
        if (_isMultiBlockDragging) Mouse.Capture(null);
        _isMultiBlockDragging = false;
        e.Handled = true;
        Dispatcher.BeginInvoke(() => ShowMultiBlockSelection(startBox, startIndex, endBox, endIndex), DispatcherPriority.Background);
    }

    private void ShowMultiBlockSelection(TextBox startBox, int startIndex, TextBox endBox, int endIndex)
    {
        _selectionSegments.Clear();
        _selectionSegments.AddRange(BuildMultiBlockSegments(startBox, startIndex, endBox, endIndex));
        if (_selectionSegments.Count == 0)
        {
            return;
        }
        var first = _selectionSegments[0];
        _selectionTarget = first.Target;
        _selectionSourceKey = first.SourceKey;
        _selectionStart = first.Start;
        _selectionLength = first.Length;
        _selectionText = string.Join(Environment.NewLine, _selectionSegments.Select(segment => segment.Text));
        _selectedRichTextRange = null;
        SelectionPasteButton.Visibility = Visibility.Collapsed;
        SelectionDeleteHighlightButton.Visibility = FindSelectedAnnotations().Any(annotation => annotation.IsHighlighted) ? Visibility.Visible : Visibility.Collapsed;
        SelectionDeleteNoteButton.Visibility = FindSelectedAnnotations().Any(annotation => !string.IsNullOrWhiteSpace(annotation.Note)) ? Visibility.Visible : Visibility.Collapsed;
        LayoutSelectionActionsOnCurve();
        CollapseSelectionActionButtons();
        SelectionActionPopup.PlacementTarget = endBox;
        SelectionActionPopup.IsOpen = true;
    }

    private List<SelectionSegment> BuildMultiBlockSegments(TextBox startBox, int startIndex, TextBox endBox, int endIndex)
    {
        var segments = new List<SelectionSegment>();
        if (_currentStudy is null || startBox.Tag is not StudyBlock startBlock || endBox.Tag is not StudyBlock endBlock)
        {
            return segments;
        }

        var startBlockIndex = _currentStudy.Blocks.IndexOf(startBlock);
        var endBlockIndex = _currentStudy.Blocks.IndexOf(endBlock);
        if (startBlockIndex < 0 || endBlockIndex < 0)
        {
            return segments;
        }

        if (startBlockIndex > endBlockIndex)
        {
            (startBlockIndex, endBlockIndex) = (endBlockIndex, startBlockIndex);
            (startBox, endBox) = (endBox, startBox);
            (startIndex, endIndex) = (endIndex, startIndex);
        }

        var boxesByBlock = FindDescendants<TextBox>(StudyBlockItems)
            .Where(box => box.Tag is StudyBlock)
            .ToDictionary(box => (StudyBlock)box.Tag);
        for (var index = startBlockIndex; index <= endBlockIndex; index++)
        {
            var block = _currentStudy.Blocks[index];
            if (!boxesByBlock.TryGetValue(block, out var box))
            {
                continue;
            }
            var segmentStart = index == startBlockIndex ? Math.Clamp(startIndex, 0, box.Text.Length) : 0;
            var segmentEnd = index == endBlockIndex ? Math.Clamp(endIndex, 0, box.Text.Length) : box.Text.Length;
            if (segmentEnd < segmentStart) (segmentStart, segmentEnd) = (segmentEnd, segmentStart);
            if (segmentEnd == segmentStart) continue;
            segments.Add(new SelectionSegment(box, $"notes:block:{index}", segmentStart,
                segmentEnd - segmentStart, box.Text.Substring(segmentStart, segmentEnd - segmentStart)));
        }
        return segments;
    }

    private void UpdateMultiBlockSelectionPreview()
    {
        if (_multiSelectStartTextBox is null || _multiSelectEndTextBox is null)
        {
            return;
        }
        var segments = BuildMultiBlockSegments(_multiSelectStartTextBox, _multiSelectStartIndex,
            _multiSelectEndTextBox, _multiSelectEndIndex);
        _multiBlockSelectionPreview.Clear();
        _multiBlockSelectionPreview.AddRange(segments.Select(segment => new TextAnnotationState
        {
            SourceKey = segment.SourceKey,
            Start = segment.Start,
            Length = segment.Length,
            Quote = segment.Text,
            IsHighlighted = true,
            HighlightColor = "#AECBFA"
        }));
        foreach (var textBox in segments.Select(segment => segment.Target).OfType<TextBox>().Distinct())
        {
            EnsureTextAnnotationAdorner(textBox).InvalidateVisual();
        }
    }

    private void ClearMultiBlockSelectionPreview()
    {
        if (_multiBlockSelectionPreview.Count == 0)
        {
            return;
        }
        _multiBlockSelectionPreview.Clear();
        foreach (var adorner in _textAnnotationAdorners.Values)
        {
            adorner.InvalidateVisual();
        }
    }

    private void SelectableText_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        Dispatcher.BeginInvoke(() => ShowSelectionActions(sender as Control), DispatcherPriority.Input);
    }

    private void SelectableText_SelectionChanged(object sender, RoutedEventArgs e)
    {
        if (_isMultiBlockDragging || !SelectionActionPopup.IsOpen || !ReferenceEquals(sender, _selectionTarget))
        {
            return;
        }

        var selectionIsEmpty = sender switch
        {
            TextBox textBox => textBox.SelectionLength == 0,
            RichTextBox richTextBox => richTextBox.Selection.IsEmpty,
            _ => false
        };
        if (selectionIsEmpty)
        {
            CloseSelectionUi();
        }
    }

    private void SelectableText_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox)
        {
            Dispatcher.BeginInvoke(() =>
            {
                if (textBox.IsLoaded)
                {
                    EnsureTextAnnotationAdorner(textBox).InvalidateVisual();
                }
            }, DispatcherPriority.Render);
        }
    }

    private void RefreshVisibleAnnotationRendering()
    {
        Dispatcher.BeginInvoke(() =>
        {
            StudyBlockItems.UpdateLayout();
            foreach (var textBox in FindDescendants<TextBox>(StudyBlockItems))
            {
                EnsureTextAnnotationAdorner(textBox).InvalidateVisual();
            }

            if (ScriptureTextView.IsVisible)
            {
                ApplySavedRichTextAnnotations(ScriptureTextView);
            }
            if (StrongsDefinitionViewer.IsVisible)
            {
                ApplySavedRichTextAnnotations(StrongsDefinitionViewer);
            }
        }, DispatcherPriority.Render);
    }

    private void ShowSelectionActions(Control? control)
    {
        _selectionSegments.Clear();
        _selectedRichTextRange = null;
        _selectionTarget = null;
        _selectionText = string.Empty;

        if (control is TextBox textBox && textBox.SelectionLength > 0)
        {
            _selectionTarget = textBox;
            _selectionStart = textBox.SelectionStart;
            _selectionLength = textBox.SelectionLength;
            _selectionText = textBox.SelectedText;
            _selectionSourceKey = GetSelectionSourceKey(textBox);
        }
        else if (control is RichTextBox richTextBox && !richTextBox.Selection.IsEmpty)
        {
            var range = new TextRange(richTextBox.Selection.Start, richTextBox.Selection.End);
            if (string.IsNullOrWhiteSpace(range.Text))
            {
                SelectionActionPopup.IsOpen = false;
                return;
            }

            _selectionTarget = richTextBox;
            _selectedRichTextRange = range;
            _selectionText = range.Text;
            _selectionStart = new TextRange(richTextBox.Document.ContentStart, range.Start).Text.Length;
            _selectionLength = range.Text.Length;
            _selectionSourceKey = GetSelectionSourceKey(richTextBox);
        }
        else
        {
            SelectionActionPopup.IsOpen = false;
            return;
        }

        _selectionSegments.Add(new SelectionSegment(_selectionTarget, _selectionSourceKey, _selectionStart, _selectionLength, _selectionText));

        var editableNotes = control is TextBox { IsReadOnly: false };
        SelectionPasteButton.Visibility = editableNotes ? Visibility.Visible : Visibility.Collapsed;
        var matching = FindSelectedAnnotations().ToList();
        SelectionDeleteHighlightButton.Visibility = matching.Any(annotation => annotation.IsHighlighted)
            ? Visibility.Visible : Visibility.Collapsed;
        SelectionDeleteNoteButton.Visibility = matching.Any(annotation => !string.IsNullOrWhiteSpace(annotation.Note))
            ? Visibility.Visible : Visibility.Collapsed;
        LayoutSelectionActionsOnCurve();
        CollapseSelectionActionButtons();
        SelectionActionPopup.PlacementTarget = control;
        SelectionActionPopup.IsOpen = true;
    }

    private string GetSelectionSourceKey(Control control)
    {
        if (control is TextBox { Tag: StudyBlock block } && _currentStudy is not null)
        {
            return $"notes:block:{_currentStudy.Blocks.IndexOf(block)}";
        }

        if (control.Tag is ExtraStudyPanelState panel)
        {
            return $"panel:{panel.Id}";
        }

        if (ReferenceEquals(control, ScriptureTextView))
        {
            return $"scripture:{_scriptureVisibleBookName}:{_scriptureVisibleChapter}";
        }

        if (ReferenceEquals(control, StrongsDefinitionViewer))
        {
            return $"strongs:{StrongsNumberText.Text}";
        }

        return control.Name;
    }

    private IEnumerable<TextAnnotationState> FindSelectedAnnotations()
    {
        if (_currentStudy is null)
        {
            return [];
        }

        var segments = _selectionSegments.Count > 0
            ? _selectionSegments
            : [new SelectionSegment(_selectionTarget!, _selectionSourceKey, _selectionStart, _selectionLength, _selectionText)];
        return _currentStudy.TextAnnotations.Where(annotation => segments.Any(segment =>
            annotation.SourceKey == segment.SourceKey
            && annotation.Start < segment.Start + segment.Length
            && annotation.Start + annotation.Length > segment.Start));
    }

    private TextAnnotationState GetOrCreateSelectedAnnotation()
    {
        var annotation = FindSelectedAnnotations().FirstOrDefault(annotation =>
            annotation.Start == _selectionStart && annotation.Length == _selectionLength);
        if (annotation is not null)
        {
            return annotation;
        }

        annotation = new TextAnnotationState
        {
            SourceKey = _selectionSourceKey,
            Start = _selectionStart,
            Length = _selectionLength,
            Quote = _selectionText
        };
        _currentStudy!.TextAnnotations.Add(annotation);
        return annotation;
    }

    private IReadOnlyList<TextAnnotationState> GetOrCreateSelectedAnnotations()
    {
        if (_selectionSegments.Count <= 1)
        {
            return [GetOrCreateSelectedAnnotation()];
        }

        var annotations = new List<TextAnnotationState>();
        foreach (var segment in _selectionSegments)
        {
            var annotation = _currentStudy!.TextAnnotations.FirstOrDefault(item => item.SourceKey == segment.SourceKey
                && item.Start == segment.Start && item.Length == segment.Length);
            if (annotation is null)
            {
                annotation = new TextAnnotationState
                {
                    SourceKey = segment.SourceKey,
                    Start = segment.Start,
                    Length = segment.Length,
                    Quote = segment.Text
                };
                _currentStudy.TextAnnotations.Add(annotation);
            }
            annotations.Add(annotation);
        }
        return annotations;
    }

    private void SelectionActionButton_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is not Button hovered)
        {
            return;
        }

        AnimateSelectionActionButtons(hovered);
    }

    private void SelectionActionButton_MouseLeave(object sender, MouseEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            var hovered = GetSelectionActionButtons().FirstOrDefault(button => button.IsMouseOver);
            AnimateSelectionActionButtons(hovered);
        }, DispatcherPriority.Input);
    }

    private void AnimateSelectionActionButtons(Button? hovered)
    {
        var ease = new SineEase { EasingMode = EasingMode.EaseOut };
        var duration = TimeSpan.FromMilliseconds(240);
        foreach (var layout in GetSelectionActionLayout())
        {
            var isHovered = ReferenceEquals(layout.Button, hovered);
            Panel.SetZIndex(layout.Button, isHovered ? 100 : 1);
            layout.Button.BeginAnimation(WidthProperty,
                new DoubleAnimation(layout.Button.ActualWidth, isHovered ? 54 : 38, duration) { EasingFunction = ease });
            layout.Button.BeginAnimation(HeightProperty,
                new DoubleAnimation(layout.Button.ActualHeight, isHovered ? 54 : 38, duration) { EasingFunction = ease });
            layout.Button.BeginAnimation(Canvas.LeftProperty,
                new DoubleAnimation(Canvas.GetLeft(layout.Button), layout.Left + (isHovered ? -8 : 0), duration) { EasingFunction = ease });
            layout.Button.BeginAnimation(Canvas.TopProperty,
                new DoubleAnimation(Canvas.GetTop(layout.Button), layout.Top + (isHovered ? -8 : 0), duration) { EasingFunction = ease });
        }
    }

    private IEnumerable<Button> GetSelectionActionButtons()
    {
        return new[]
        {
            SelectionHighlightButton,
            SelectionCopyButton,
            SelectionPasteButton,
            SelectionAddNoteButton,
            SelectionDeleteHighlightButton,
            SelectionDeleteNoteButton
        }.Where(button => button.Visibility == Visibility.Visible);
    }

    private List<(Button Button, double Left, double Top)> GetSelectionActionLayout()
    {
        var visibleButtons = GetSelectionActionButtons().ToList();
        var layout = new List<(Button Button, double Left, double Top)>();
        var curveExtent = Math.Clamp(0.4 + 0.12 * Math.Max(0, visibleButtons.Count - 1), 0.4, 1);
        UpdateSelectionActionCurveGeometry(curveExtent);

        for (var index = 0; index < visibleButtons.Count; index++)
        {
            var t = visibleButtons.Count == 1 ? 0 : curveExtent * index / (visibleButtons.Count - 1);
            var inverse = 1 - t;
            var x = inverse * inverse * inverse * 30
                    + 3 * inverse * inverse * t * 80
                    + 3 * inverse * t * t * 145
                    + t * t * t * 175;
            var y = inverse * inverse * inverse * 35
                    + 3 * inverse * inverse * t * 35
                    + 3 * inverse * t * t * 65
                    + t * t * t * 105;
            layout.Add((visibleButtons[index], x - 19, y - 19));
        }

        return layout;
    }

    private void UpdateSelectionActionCurveGeometry(double extent)
    {
        var p0 = new Point(30, 35);
        var p1 = new Point(80, 35);
        var p2 = new Point(145, 65);
        var p3 = new Point(175, 105);
        var q0 = Lerp(p0, p1, extent);
        var q1 = Lerp(p1, p2, extent);
        var q2 = Lerp(p2, p3, extent);
        var r0 = Lerp(q0, q1, extent);
        var r1 = Lerp(q1, q2, extent);
        var end = Lerp(r0, r1, extent);
        SelectionActionCurve.Data = new PathGeometry([
            new PathFigure(p0, [new BezierSegment(q0, r0, end, true)], false)
        ]);
    }

    private static Point Lerp(Point start, Point end, double amount)
    {
        return new Point(start.X + (end.X - start.X) * amount, start.Y + (end.Y - start.Y) * amount);
    }

    private void LayoutSelectionActionsOnCurve()
    {
        foreach (var layout in GetSelectionActionLayout())
        {
            layout.Button.BeginAnimation(Canvas.LeftProperty, null);
            layout.Button.BeginAnimation(Canvas.TopProperty, null);
            Canvas.SetLeft(layout.Button, layout.Left);
            Canvas.SetTop(layout.Button, layout.Top);
        }
    }

    private void CollapseSelectionActionButtons()
    {
        foreach (var button in new[] { SelectionHighlightButton, SelectionCopyButton, SelectionPasteButton,
                     SelectionAddNoteButton, SelectionDeleteHighlightButton, SelectionDeleteNoteButton })
        {
            Panel.SetZIndex(button, 1);
            button.BeginAnimation(WidthProperty, null);
            button.BeginAnimation(HeightProperty, null);
            button.BeginAnimation(Canvas.LeftProperty, null);
            button.BeginAnimation(Canvas.TopProperty, null);
            button.Width = 38;
            button.Height = 38;
        }
        LayoutSelectionActionsOnCurve();
    }

    private void SelectionHighlight_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStudy is null || _selectionTarget is null)
        {
            return;
        }

        HighlightColorPopup.IsOpen = true;
    }

    private void HighlightColor_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string color } || _currentStudy is null || _selectionTarget is null)
        {
            return;
        }

        foreach (var annotation in GetOrCreateSelectedAnnotations())
        {
            annotation.IsHighlighted = true;
            annotation.HighlightColor = color;
        }
        _suppressSelectionSubmenuClose = true;
        HighlightColorPopup.IsOpen = false;
        ApplyCurrentSelectionFormatting();
        FinishAnnotationChange("Highlight added");
        _suppressSelectionSubmenuClose = false;
    }

    private void SelectionActionMenu_MouseLeave(object sender, MouseEventArgs e)
    {
        AnimateSelectionActionButtons(null);
    }

    private void SelectionCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_selectionText))
        {
            Clipboard.SetText(_selectionText);
            ShowToast("Copied");
        }
        CloseSelectionUi();
    }

    private void SelectionPaste_Click(object sender, RoutedEventArgs e)
    {
        if (_selectionTarget is TextBox textBox && Clipboard.ContainsText())
        {
            var value = Clipboard.GetText();
            textBox.SelectedText = value;
            textBox.CaretIndex = _selectionStart + value.Length;
            QueueWorkspaceSave();
        }
        CloseSelectionUi();
    }

    private void SelectionAddNote_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStudy is null || _selectionTarget is null)
        {
            return;
        }

        var existing = FindSelectedAnnotations().FirstOrDefault(annotation => !string.IsNullOrWhiteSpace(annotation.Note));
        _pendingNoteAnnotations = GetOrCreateSelectedAnnotations();
        AnnotationNoteTextBox.Text = existing?.Note ?? string.Empty;
        AnnotationNotePopup.IsOpen = true;
        AnimateAnnotationNoteCard();
    }

    private void AnimateAnnotationNoteCard()
    {
        var hiddenOpacity = (double)FindResource("FloatingSurfaceHiddenOpacity");
        var hiddenScale = (double)FindResource("FloatingSurfaceHiddenScale");
        var hiddenOffset = (double)FindResource("FloatingSurfaceHiddenOffset");
        var fadeDuration = (Duration)FindResource("FloatingSurfaceFadeDuration");
        var motionDuration = (Duration)FindResource("FloatingSurfaceMotionDuration");
        var ease = (IEasingFunction)FindResource("FloatingSurfaceEntranceEase");
        AnnotationNoteCard.BeginAnimation(OpacityProperty, null);
        AnnotationNoteCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        AnnotationNoteCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        AnnotationNoteCardTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        AnnotationNoteCard.Opacity = hiddenOpacity;
        AnnotationNoteCardScale.ScaleX = hiddenScale;
        AnnotationNoteCardScale.ScaleY = hiddenScale;
        AnnotationNoteCardTranslate.Y = hiddenOffset;
        AnnotationNoteCard.BeginAnimation(OpacityProperty,
            new DoubleAnimation(hiddenOpacity, 1, fadeDuration) { EasingFunction = ease });
        AnnotationNoteCardScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(hiddenScale, 1, motionDuration) { EasingFunction = ease });
        AnnotationNoteCardScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(hiddenScale, 1, motionDuration) { EasingFunction = ease });
        AnnotationNoteCardTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(hiddenOffset, 0, motionDuration) { EasingFunction = ease });
        Dispatcher.BeginInvoke(() =>
        {
            AnnotationNoteTextBox.Focus();
            AnnotationNoteTextBox.CaretIndex = AnnotationNoteTextBox.Text.Length;
        }, DispatcherPriority.Input);
    }

    private void AnnotationNoteSave_Click(object sender, RoutedEventArgs e)
    {
        var note = AnnotationNoteTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            ShowToast("Write a note first");
            return;
        }

        foreach (var annotation in _pendingNoteAnnotations)
        {
            annotation.Note = note;
        }
        _suppressSelectionSubmenuClose = true;
        AnnotationNotePopup.IsOpen = false;
        ApplyCurrentSelectionFormatting();
        FinishAnnotationChange("Note attached");
        _suppressSelectionSubmenuClose = false;
        _pendingNoteAnnotations = [];
    }

    private void AnnotationNoteCancel_Click(object sender, RoutedEventArgs e)
    {
        AnnotationNotePopup.IsOpen = false;
    }

    private void AnnotationNotePopup_Closed(object? sender, EventArgs e)
    {
        foreach (var annotation in _pendingNoteAnnotations.Where(annotation =>
                     !annotation.IsHighlighted && string.IsNullOrWhiteSpace(annotation.Note)).ToList())
        {
            _currentStudy?.TextAnnotations.Remove(annotation);
        }
        _pendingNoteAnnotations = [];
        if (!_isClosingSelectionUi && !_suppressSelectionSubmenuClose)
        {
            CloseSelectionUi();
        }
    }

    private void SelectionSubmenu_Closed(object? sender, EventArgs e)
    {
        if (!_isClosingSelectionUi && !_suppressSelectionSubmenuClose)
        {
            CloseSelectionUi();
        }
    }

    private void SelectionDeleteHighlight_Click(object sender, RoutedEventArgs e)
    {
        foreach (var annotation in FindSelectedAnnotations().ToList())
        {
            annotation.IsHighlighted = false;
            RemoveEmptyAnnotation(annotation);
        }
        RefreshSelectionFormatting();
        FinishAnnotationChange("Highlight removed");
    }

    private void SelectionDeleteNote_Click(object sender, RoutedEventArgs e)
    {
        foreach (var annotation in FindSelectedAnnotations().ToList())
        {
            annotation.Note = string.Empty;
            RemoveEmptyAnnotation(annotation);
        }
        RefreshSelectionFormatting();
        FinishAnnotationChange("Note deleted");
    }

    private void RemoveEmptyAnnotation(TextAnnotationState annotation)
    {
        if (!annotation.IsHighlighted && string.IsNullOrWhiteSpace(annotation.Note))
        {
            _currentStudy?.TextAnnotations.Remove(annotation);
        }
    }

    private void ApplyCurrentSelectionFormatting()
    {
        if (_selectedRichTextRange is not null)
        {
            var annotation = FindSelectedAnnotations().FirstOrDefault(item =>
                item.Start == _selectionStart && item.Length == _selectionLength);
            if (annotation is null)
            {
                return;
            }
            if (annotation.IsHighlighted)
            {
                var color = ParseAnnotationColor(annotation.HighlightColor);
                _selectedRichTextRange.ApplyPropertyValue(TextElement.BackgroundProperty,
                    new SolidColorBrush(Color.FromArgb(105, color.R, color.G, color.B)));
            }
            if (!string.IsNullOrWhiteSpace(annotation.Note))
            {
                _selectedRichTextRange.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
            }
        }
        else if (_selectionTarget is TextBox textBox)
        {
            foreach (var target in _selectionSegments.Select(segment => segment.Target).OfType<TextBox>().DefaultIfEmpty(textBox).Distinct())
            {
                EnsureTextAnnotationAdorner(target).InvalidateVisual();
            }
        }
    }

    private void ApplySavedRichTextAnnotations(RichTextBox viewer)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var sourceKey = GetSelectionSourceKey(viewer);
        var annotations = _currentStudy.TextAnnotations.Where(annotation => annotation.SourceKey == sourceKey).ToList();
        foreach (var annotation in annotations)
        {
            var start = GetTextPointerAtTextOffset(viewer.Document, annotation.Start);
            var end = GetTextPointerAtTextOffset(viewer.Document, annotation.Start + annotation.Length);
            if (start is null || end is null || start.CompareTo(end) >= 0)
            {
                continue;
            }

            var range = new TextRange(start, end);
            if (annotation.IsHighlighted)
            {
                var color = ParseAnnotationColor(annotation.HighlightColor);
                range.ApplyPropertyValue(TextElement.BackgroundProperty,
                    new SolidColorBrush(Color.FromArgb(105, color.R, color.G, color.B)));
            }
            if (!string.IsNullOrWhiteSpace(annotation.Note))
            {
                range.ApplyPropertyValue(Inline.TextDecorationsProperty, TextDecorations.Underline);
            }
        }
    }

    private static TextPointer? GetTextPointerAtTextOffset(FlowDocument document, int textOffset)
    {
        var documentStart = document.ContentStart;
        var documentEnd = document.ContentEnd;
        var totalTextLength = new TextRange(documentStart, documentEnd).Text.Length;
        if (textOffset < 0 || textOffset > totalTextLength)
        {
            return null;
        }

        // TextPointer offsets count formatting symbols as well as visible characters.
        // Binary-search those symbols using TextRange.Text so persisted character offsets
        // continue to map correctly across runs, line breaks, and paragraphs.
        var low = 0;
        var high = documentStart.GetOffsetToPosition(documentEnd);
        TextPointer? match = textOffset == 0 ? documentStart : null;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candidate = documentStart.GetPositionAtOffset(middle);
            if (candidate is null)
            {
                high = middle - 1;
                continue;
            }

            var candidateTextLength = new TextRange(documentStart, candidate).Text.Length;
            if (candidateTextLength < textOffset)
            {
                low = middle + 1;
            }
            else
            {
                if (candidateTextLength == textOffset)
                {
                    match = candidate;
                }
                high = middle - 1;
            }
        }

        return match;
    }

    private static IEnumerable<Run> FindRuns(Block block)
    {
        if (block is Paragraph paragraph)
        {
            foreach (var inline in paragraph.Inlines)
            {
                foreach (var run in FindRuns(inline))
                {
                    yield return run;
                }
            }
        }
        else if (block is Section section)
        {
            foreach (var child in section.Blocks)
            {
                foreach (var run in FindRuns(child))
                {
                    yield return run;
                }
            }
        }
    }

    private static IEnumerable<Run> FindRuns(Inline inline)
    {
        if (inline is Run run)
        {
            yield return run;
        }
        else if (inline is Span span)
        {
            foreach (var child in span.Inlines)
            {
                foreach (var nested in FindRuns(child))
                {
                    yield return nested;
                }
            }
        }
    }

    private Color ParseAnnotationColor(string value)
    {
        try
        {
            return (Color)ColorConverter.ConvertFromString(value);
        }
        catch
        {
            return ((SolidColorBrush)GetResourceBrush("Gold")).Color;
        }
    }

    private void RefreshSelectionFormatting()
    {
        if (_selectedRichTextRange is not null)
        {
            _selectedRichTextRange.ApplyPropertyValue(TextElement.BackgroundProperty, Brushes.Transparent);
            _selectedRichTextRange.ApplyPropertyValue(Inline.TextDecorationsProperty, null);
            ApplyCurrentSelectionFormatting();
        }
        else if (_selectionTarget is TextBox textBox && _textAnnotationAdorners.TryGetValue(textBox, out var adorner))
        {
            adorner.InvalidateVisual();
            foreach (var target in _selectionSegments.Select(segment => segment.Target).OfType<TextBox>())
            {
                EnsureTextAnnotationAdorner(target).InvalidateVisual();
            }
        }
    }

    private void FinishAnnotationChange(string toast)
    {
        SaveWorkspaceState();
        CloseSelectionUi();
        ShowToast(toast);
    }

    private void CloseSelectionUi()
    {
        if (_isClosingSelectionUi)
        {
            return;
        }

        _isClosingSelectionUi = true;
        try
        {
            HighlightColorPopup.IsOpen = false;
            AnnotationNotePopup.IsOpen = false;
            SelectionActionPopup.IsOpen = false;
            SavedNotePopup.IsOpen = false;
            CollapseSelectionActionButtons();
            ClearMultiBlockSelectionPreview();
            _selectionSegments.Clear();
            _selectionTarget = null;
            _selectedRichTextRange = null;
            _selectionText = string.Empty;
            _selectionSourceKey = string.Empty;
            _selectionStart = 0;
            _selectionLength = 0;
            _multiSelectStartTextBox = null;
            _multiSelectEndTextBox = null;
            if (_isMultiBlockDragging)
            {
                Mouse.Capture(null);
                _isMultiBlockDragging = false;
            }
        }
        finally
        {
            _isClosingSelectionUi = false;
        }
    }

    private TextAnnotationAdorner EnsureTextAnnotationAdorner(TextBox textBox)
    {
        if (_textAnnotationAdorners.TryGetValue(textBox, out var existing))
        {
            return existing;
        }
        var layer = AdornerLayer.GetAdornerLayer(textBox);
        var adorner = new TextAnnotationAdorner(textBox, () =>
        {
            var sourceKey = GetSelectionSourceKey(textBox);
            var saved = _currentStudy?.TextAnnotations.Where(annotation => annotation.SourceKey == sourceKey) ?? [];
            return saved.Concat(_multiBlockSelectionPreview.Where(annotation => annotation.SourceKey == sourceKey)).ToList();
        });
        layer?.Add(adorner);
        _textAnnotationAdorners[textBox] = adorner;
        textBox.TextChanged += (_, _) => adorner.InvalidateVisual();
        textBox.SizeChanged += (_, _) => adorner.InvalidateVisual();
        return adorner;
    }

    private void SelectableText_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_currentStudy is null || sender is not Control control)
        {
            return;
        }
        var offset = control switch
        {
            TextBox textBox => textBox.GetCharacterIndexFromPoint(e.GetPosition(textBox), true),
            RichTextBox rich => new TextRange(rich.Document.ContentStart, rich.GetPositionFromPoint(e.GetPosition(rich), true)).Text.Length,
            _ => -1
        };
        var annotation = _currentStudy.TextAnnotations.FirstOrDefault(item => item.SourceKey == GetSelectionSourceKey(control)
            && offset >= item.Start && offset <= item.Start + item.Length && !string.IsNullOrWhiteSpace(item.Note));
        if (annotation is null)
        {
            ScheduleSavedNoteClose();
            return;
        }
        _savedNoteCloseTimer.Stop();
        if (SavedNotePopup.IsOpen && ReferenceEquals(annotation, _savedNoteAnnotation))
        {
            return;
        }

        _savedNoteAnnotation = annotation;
        _savedNoteTarget = control;
        _isEditingSavedNote = false;
        SavedNoteTextBox.Text = annotation.Note;
        SetSavedNoteEditMode(false);
        SavedNotePopup.PlacementTarget = control;
        SavedNotePopup.IsOpen = true;
        AnimateSavedNoteCard();
    }

    private void AnimateSavedNoteCard()
    {
        var hiddenOpacity = (double)FindResource("FloatingSurfaceHiddenOpacity");
        var hiddenScale = (double)FindResource("FloatingSurfaceHiddenScale");
        var hiddenOffset = (double)FindResource("FloatingSurfaceHiddenOffset");
        var fadeDuration = (Duration)FindResource("FloatingSurfaceFadeDuration");
        var motionDuration = (Duration)FindResource("FloatingSurfaceMotionDuration");
        var ease = (IEasingFunction)FindResource("FloatingSurfaceEntranceEase");

        SavedNoteCard.BeginAnimation(OpacityProperty, null);
        SavedNoteCardScale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
        SavedNoteCardScale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        SavedNoteCardTranslate.BeginAnimation(TranslateTransform.YProperty, null);
        SavedNoteCard.Opacity = hiddenOpacity;
        SavedNoteCardScale.ScaleX = hiddenScale;
        SavedNoteCardScale.ScaleY = hiddenScale;
        SavedNoteCardTranslate.Y = hiddenOffset;
        SavedNoteCard.BeginAnimation(OpacityProperty,
            new DoubleAnimation(hiddenOpacity, 1, fadeDuration) { EasingFunction = ease });
        SavedNoteCardScale.BeginAnimation(ScaleTransform.ScaleXProperty,
            new DoubleAnimation(hiddenScale, 1, motionDuration) { EasingFunction = ease });
        SavedNoteCardScale.BeginAnimation(ScaleTransform.ScaleYProperty,
            new DoubleAnimation(hiddenScale, 1, motionDuration) { EasingFunction = ease });
        SavedNoteCardTranslate.BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(hiddenOffset, 0, motionDuration) { EasingFunction = ease });
    }

    private void ScheduleSavedNoteClose()
    {
        if (!SavedNotePopup.IsOpen || _isEditingSavedNote)
        {
            return;
        }
        _savedNoteCloseTimer.Stop();
        _savedNoteCloseTimer.Start();
    }

    private void SavedNoteCard_MouseEnter(object sender, MouseEventArgs e) => _savedNoteCloseTimer.Stop();

    private void SavedNoteCard_MouseLeave(object sender, MouseEventArgs e) => ScheduleSavedNoteClose();

    private void SetSavedNoteEditMode(bool editing)
    {
        _isEditingSavedNote = editing;
        SavedNoteTextBox.IsReadOnly = !editing;
        SavedNoteViewActions.Visibility = editing ? Visibility.Collapsed : Visibility.Visible;
        SavedNoteEditActions.Visibility = editing ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SavedNoteEdit_Click(object sender, RoutedEventArgs e)
    {
        _savedNoteCloseTimer.Stop();
        SetSavedNoteEditMode(true);
        SavedNoteTextBox.Focus();
        SavedNoteTextBox.CaretIndex = SavedNoteTextBox.Text.Length;
    }

    private void SavedNoteEditCancel_Click(object sender, RoutedEventArgs e)
    {
        SavedNoteTextBox.Text = _savedNoteAnnotation?.Note ?? string.Empty;
        SetSavedNoteEditMode(false);
    }

    private void SavedNoteSave_Click(object sender, RoutedEventArgs e)
    {
        if (_savedNoteAnnotation is null)
        {
            return;
        }
        var note = SavedNoteTextBox.Text.Trim();
        if (string.IsNullOrWhiteSpace(note))
        {
            ShowToast("Write a note first");
            return;
        }

        _savedNoteAnnotation.Note = note;
        SaveWorkspaceState();
        RefreshSavedNoteTarget();
        SetSavedNoteEditMode(false);
        ShowToast("Note updated");
    }

    private void SavedNoteDelete_Click(object sender, RoutedEventArgs e)
    {
        if (_savedNoteAnnotation is null)
        {
            return;
        }

        ClearSavedNoteUnderline(_savedNoteAnnotation);
        _savedNoteAnnotation.Note = string.Empty;
        RemoveEmptyAnnotation(_savedNoteAnnotation);
        SaveWorkspaceState();
        RefreshSavedNoteTarget();
        SavedNotePopup.IsOpen = false;
        ShowToast("Note deleted");
    }

    private void ClearSavedNoteUnderline(TextAnnotationState annotation)
    {
        if (_savedNoteTarget is not RichTextBox richTextBox)
        {
            return;
        }

        var start = GetTextPointerAtTextOffset(richTextBox.Document, annotation.Start);
        var end = GetTextPointerAtTextOffset(richTextBox.Document, annotation.Start + annotation.Length);
        if (start is not null && end is not null && start.CompareTo(end) < 0)
        {
            new TextRange(start, end).ApplyPropertyValue(Inline.TextDecorationsProperty, null);
        }
    }

    private void RefreshSavedNoteTarget()
    {
        if (_savedNoteTarget is TextBox textBox)
        {
            EnsureTextAnnotationAdorner(textBox).InvalidateVisual();
        }
        else if (_savedNoteTarget is RichTextBox richTextBox)
        {
            ApplySavedRichTextAnnotations(richTextBox);
        }
    }

    private void SavedNotePopup_Closed(object? sender, EventArgs e)
    {
        _savedNoteCloseTimer.Stop();
        _savedNoteAnnotation = null;
        _savedNoteTarget = null;
        _isEditingSavedNote = false;
    }

    private void AddExtraScripturePanel(string bookName, int chapter, int? selectedVerse, string subtitle)
    {
        if (_currentStudy is null)
        {
            return;
        }

        var panel = new ExtraStudyPanelState
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = ExtraStudyPanelKind.Scripture,
            Title = $"{bookName} {chapter}",
            BookName = bookName,
            Chapter = chapter,
            SelectedVerse = selectedVerse,
            Geometry = CreateDefaultExtraPanelGeometry(_currentStudy.ExtraPanels.Count, width: 430, height: 520)
        };
        _currentStudy.ExtraPanels.Add(panel);
        RenderExtraStudyPanel(panel, animate: true);
        QueueWorkspaceSave();
        ShowToast(string.IsNullOrWhiteSpace(subtitle) ? "Scripture panel added" : "Scripture panel opened");
    }

    private void AddExtraNotesPanel()
    {
        if (_currentStudy is null)
        {
            return;
        }

        var panel = new ExtraStudyPanelState
        {
            Id = Guid.NewGuid().ToString("N"),
            Kind = ExtraStudyPanelKind.Notes,
            Title = "Notes",
            Geometry = CreateDefaultExtraPanelGeometry(_currentStudy.ExtraPanels.Count, width: 420, height: 360)
        };
        _currentStudy.ExtraPanels.Add(panel);
        RenderExtraStudyPanel(panel, animate: true);
        QueueWorkspaceSave();
        ShowToast("Notes panel added");
    }

    private PanelGeometryState CreateDefaultExtraPanelGeometry(int index, double width, double height)
    {
        var maxWidth = Math.Max(1, StudyPanel.ActualWidth);
        var maxHeight = Math.Max(1, StudyPanel.ActualHeight);
        var safeWidth = Math.Min(width, maxWidth);
        var safeHeight = Math.Min(height, maxHeight);
        var offset = 34 * (index % 7);
        return new PanelGeometryState
        {
            X = Math.Round(Math.Clamp(48 + offset, 0, Math.Max(0, maxWidth - safeWidth)), 2),
            Y = Math.Round(Math.Clamp(56 + offset, 0, Math.Max(0, maxHeight - safeHeight)), 2),
            Width = Math.Round(safeWidth, 2),
            Height = Math.Round(safeHeight, 2)
        };
    }

    private void RenderExtraStudyPanels()
    {
        foreach (var runtime in _extraPanelRuntimes.Values.ToList())
        {
            ExtraStudyPanelLayer.Children.Remove(runtime.Root);
        }

        _extraPanelRuntimes.Clear();
        if (_currentStudy is null)
        {
            return;
        }

        foreach (var panel in _currentStudy.ExtraPanels)
        {
            RenderExtraStudyPanel(panel, animate: false);
        }
    }

    private void RenderExtraStudyPanel(ExtraStudyPanelState state, bool animate)
    {
        if (_extraPanelRuntimes.Remove(state.Id, out var existing))
        {
            ExtraStudyPanelLayer.Children.Remove(existing.Root);
        }

        var transform = new TranslateTransform();
        var scale = new ScaleTransform(1, 1);
        var root = new Border
        {
            Width = Math.Max(1, state.Geometry.Width),
            Height = Math.Max(1, state.Geometry.Height),
            MinWidth = 1,
            MinHeight = 1,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = state.Kind == ExtraStudyPanelKind.Notes ? GetResourceBrush("AppBackground") : GetResourceBrush("TextPrimary"),
            BorderBrush = GetResourceBrush("SidebarBackground"),
            BorderThickness = new Thickness(1.5),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(0),
            UseLayoutRounding = true,
            SnapsToDevicePixels = true,
            Tag = state,
            RenderTransformOrigin = new Point(0.5, 0.5),
            RenderTransform = new TransformGroup
            {
                Children =
                {
                    scale,
                    transform
                }
            }
        };
        Grid.SetColumn(root, 0);
        Grid.SetColumnSpan(root, 5);
        Grid.SetRow(root, 0);
        Grid.SetRowSpan(root, 2);
        Panel.SetZIndex(root, ++_topPanelZIndex);

        var geometry = ClampExtraPanelGeometry(state.Geometry);
        state.Geometry = geometry;
        root.Width = geometry.Width;
        root.Height = geometry.Height;
        transform.X = geometry.X;
        transform.Y = geometry.Y;

        var grid = new Grid();
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        var header = CreateExtraPanelHeader(state);
        header.MouseLeftButtonDown += ExtraPanelHeader_MouseLeftButtonDown;
        header.MouseMove += ExtraPanelHeader_MouseMove;
        header.MouseLeftButtonUp += ExtraPanelHeader_MouseLeftButtonUp;
        Grid.SetRow(header, 0);
        grid.Children.Add(header);

        var content = state.Kind == ExtraStudyPanelKind.Scripture
            ? CreateExtraScriptureContent(state)
            : CreateExtraNotesContent(state);
        Grid.SetRow(content, 1);
        grid.Children.Add(content);

        var resize = new Thumb
        {
            Width = 22,
            Height = 22,
            Cursor = Cursors.SizeNWSE,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(0, 0, -8, -8),
            Background = Brushes.Transparent,
            Tag = state
        };
        resize.DragStarted += ExtraPanelResize_DragStarted;
        resize.DragDelta += ExtraPanelResize_DragDelta;
        resize.DragCompleted += ExtraPanelResize_DragCompleted;
        Grid.SetRowSpan(resize, 2);
        Panel.SetZIndex(resize, 40);
        grid.Children.Add(resize);

        root.Child = grid;
        root.PreviewMouseDown += ExtraPanelRoot_PreviewMouseDown;
        ExtraStudyPanelLayer.Children.Add(root);
        _extraPanelRuntimes[state.Id] = new ExtraPanelRuntime(state, root, transform);

        if (animate)
        {
            root.Opacity = 0;
            scale.ScaleX = 0.96;
            scale.ScaleY = 0.96;
            var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
            root.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.96, 1, TimeSpan.FromMilliseconds(180)) { EasingFunction = ease });
        }
    }


    private Grid CreateExtraPanelHeader(ExtraStudyPanelState state)
    {
        var header = new Grid
        {
            Background = GetResourceBrush("PanelBackground"),
            Cursor = Cursors.SizeAll,
            MinHeight = 44,
            Tag = state
        };
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        header.Children.Add(new TextBlock
        {
            Text = state.Kind == ExtraStudyPanelKind.Scripture ? $"{state.BookName} {state.Chapter}" : state.Title,
            Foreground = GetResourceBrush("TextPrimary"),
            FontSize = 15,
            FontWeight = FontWeights.Black,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(12, 0, 10, 0),
            TextTrimming = TextTrimming.CharacterEllipsis
        });

        var close = new Button
        {
            Style = (Style)FindResource("IconButtonStyle"),
            Background = GetResourceBrush("TextSecondary"),
            Foreground = GetResourceBrush("TextPrimary"),
            Width = 38,
            Height = 34,
            MinWidth = 38,
            Padding = new Thickness(0),
            Margin = new Thickness(0, 5, 8, 5),
            ToolTip = "Close panel",
            Tag = state,
            Content = new TextBlock { Text = "X", FontSize = 13, FontWeight = FontWeights.Black }
        };
        close.Click += ExtraPanelClose_Click;
        Grid.SetColumn(close, 1);
        header.Children.Add(close);
        return header;
    }

    private UIElement CreateExtraScriptureContent(ExtraStudyPanelState state)
    {
        var document = new FlowDocument
        {
            PagePadding = new Thickness(0),
            FontFamily = new FontFamily("Segoe UI"),
            FontSize = ScriptureFontSizeSlider.Value,
            TextAlignment = TextAlignment.Left,
            Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17))
        };

        if (!string.IsNullOrWhiteSpace(state.BookName)
            && state.Chapter is int chapter
            && TryGetChapterVerses(state.BookName, chapter, out var verses))
        {
            for (var index = 0; index < verses.Count; index++)
            {
                var verseNumber = index + 1;
                var paragraph = new Paragraph { Margin = new Thickness(0, 0, 0, 10), LineHeight = 24 };
                paragraph.Inlines.Add(new Run($"{verseNumber} ")
                {
                    FontWeight = FontWeights.Black,
                    Foreground = GetResourceBrush("PanelBackground")
                });
                paragraph.Inlines.Add(new Run(verses[index]));
                if (state.SelectedVerse == verseNumber)
                {
                    paragraph.Background = GetResourceBrush("TextSecondary");
                }

                document.Blocks.Add(paragraph);
            }
        }
        else
        {
            document.Blocks.Add(new Paragraph(new Run("This chapter is not available in the current scripture data.")));
        }

        var viewer = new RichTextBox(document)
        {
            IsReadOnly = true,
            IsDocumentEnabled = true,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17)),
            SelectionBrush = GetResourceBrush("Mint"),
            SelectionOpacity = 0.45,
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Tag = state
        };
        viewer.PreviewMouseLeftButtonUp += SelectableText_PreviewMouseLeftButtonUp;
        viewer.PreviewMouseMove += SelectableText_PreviewMouseMove;
        viewer.SelectionChanged += SelectableText_SelectionChanged;
        viewer.Loaded += (_, _) => ApplySavedRichTextAnnotations(viewer);
        return viewer;
    }

    private UIElement CreateExtraNotesContent(ExtraStudyPanelState state)
    {
        var textBox = new TextBox
        {
            Text = state.NotesText,
            AcceptsReturn = true,
            TextWrapping = TextWrapping.Wrap,
            BorderThickness = new Thickness(0),
            Background = Brushes.Transparent,
            Foreground = GetResourceBrush("TextPrimary"),
            CaretBrush = GetResourceBrush("Mint"),
            FontSize = 16,
            FontFamily = new FontFamily("Segoe UI"),
            Padding = new Thickness(14),
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Tag = state
        };
        textBox.TextChanged += ExtraNotesTextBox_TextChanged;
        textBox.PreviewMouseLeftButtonUp += SelectableText_PreviewMouseLeftButtonUp;
        textBox.PreviewMouseMove += SelectableText_PreviewMouseMove;
        textBox.SelectionChanged += SelectableText_SelectionChanged;
        textBox.Loaded += (_, _) => EnsureTextAnnotationAdorner(textBox).InvalidateVisual();
        return textBox;
    }

    private PanelGeometryState ClampExtraPanelGeometry(PanelGeometryState geometry)
    {
        var maxWidth = Math.Max(1, StudyPanel.ActualWidth);
        var maxHeight = Math.Max(1, StudyPanel.ActualHeight);
        var width = Math.Round(Math.Clamp(geometry.Width <= 0 ? 360 : geometry.Width, 1, maxWidth));
        var height = Math.Round(Math.Clamp(geometry.Height <= 0 ? 320 : geometry.Height, 1, maxHeight));
        return new PanelGeometryState
        {
            X = Math.Round(Math.Clamp(geometry.X, 0, Math.Max(0, maxWidth - width)), 2),
            Y = Math.Round(Math.Clamp(geometry.Y, 0, Math.Max(0, maxHeight - height)), 2),
            Width = width,
            Height = height
        };
    }

    private void ExtraPanelRoot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: ExtraStudyPanelState state } && _extraPanelRuntimes.TryGetValue(state.Id, out var runtime))
        {
            Panel.SetZIndex(runtime.Root, ++_topPanelZIndex);
        }
    }

    private void ExtraPanelClose_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStudy is null || sender is not FrameworkElement { Tag: ExtraStudyPanelState state })
        {
            return;
        }

        _currentStudy.ExtraPanels.Remove(state);
        if (_extraPanelRuntimes.Remove(state.Id, out var runtime))
        {
            var fade = new DoubleAnimation(runtime.Root.Opacity, 0, TimeSpan.FromMilliseconds(110))
            {
                EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
            };
            fade.Completed += (_, _) => ExtraStudyPanelLayer.Children.Remove(runtime.Root);
            runtime.Root.BeginAnimation(OpacityProperty, fade);
        }

        QueueWorkspaceSave();
    }

    private void ExtraNotesTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox { Tag: ExtraStudyPanelState state } textBox)
        {
            state.NotesText = textBox.Text;
            QueueWorkspaceSave();
        }
    }

    private void ExtraPanelHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ExtraStudyPanelState state }
            || !_extraPanelRuntimes.TryGetValue(state.Id, out var runtime))
        {
            return;
        }

        _draggingExtraPanel = runtime;
        _extraPanelDragStartPoint = e.GetPosition(StudyPanel);
        _extraPanelDragStartOffset = new Point(runtime.Transform.X, runtime.Transform.Y);
        Panel.SetZIndex(runtime.Root, ++_topPanelZIndex);
        runtime.Root.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private void ExtraPanelHeader_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingExtraPanel is null || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pointer = e.GetPosition(StudyPanel);
        var delta = pointer - _extraPanelDragStartPoint;
        var geometry = ClampExtraPanelGeometry(new PanelGeometryState
        {
            X = _extraPanelDragStartOffset.X + delta.X,
            Y = _extraPanelDragStartOffset.Y + delta.Y,
            Width = _draggingExtraPanel.Root.Width,
            Height = _draggingExtraPanel.Root.Height
        });
        _draggingExtraPanel.Transform.X = geometry.X;
        _draggingExtraPanel.Transform.Y = geometry.Y;
        _draggingExtraPanel.State.Geometry = geometry;
        e.Handled = true;
    }

    private void ExtraPanelHeader_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingExtraPanel is null)
        {
            return;
        }

        _draggingExtraPanel.Root.ReleaseMouseCapture();
        _draggingExtraPanel = null;
        Mouse.OverrideCursor = null;
        QueueWorkspaceSave();
        e.Handled = true;
    }

    private void ExtraPanelResize_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: ExtraStudyPanelState state }
            || !_extraPanelRuntimes.TryGetValue(state.Id, out var runtime))
        {
            return;
        }

        _resizingExtraPanel = runtime;
        _extraPanelResizeStartPointer = Mouse.GetPosition(StudyPanel);
        _extraPanelResizeStartBounds = new Rect(runtime.Transform.X, runtime.Transform.Y, runtime.Root.Width, runtime.Root.Height);
        Panel.SetZIndex(runtime.Root, ++_topPanelZIndex);
    }

    private void ExtraPanelResize_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (_resizingExtraPanel is null)
        {
            return;
        }

        var pointer = Mouse.GetPosition(StudyPanel);
        var delta = pointer - _extraPanelResizeStartPointer;
        var geometry = ClampExtraPanelGeometry(new PanelGeometryState
        {
            X = _extraPanelResizeStartBounds.Left,
            Y = _extraPanelResizeStartBounds.Top,
            Width = _extraPanelResizeStartBounds.Width + delta.X,
            Height = _extraPanelResizeStartBounds.Height + delta.Y
        });
        _resizingExtraPanel.Root.Width = geometry.Width;
        _resizingExtraPanel.Root.Height = geometry.Height;
        _resizingExtraPanel.Transform.X = geometry.X;
        _resizingExtraPanel.Transform.Y = geometry.Y;
        _resizingExtraPanel.State.Geometry = geometry;
    }

    private void ExtraPanelResize_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        _resizingExtraPanel = null;
        QueueWorkspaceSave();
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private void ShowStrongsPanel(StrongsSelection selection)
    {
        var entry = selection.Entry;
        var lexiconEntry = FindGreekLexiconEntry(entry);
        StrongsPanelReferenceText.Text = $"{selection.BookName} {selection.Chapter}:{selection.Verse}";
        StrongsGreekText.Text = !string.IsNullOrWhiteSpace(lexiconEntry?.Greek)
            ? $"{entry.Greek}\n{lexiconEntry.Greek}"
            : string.IsNullOrWhiteSpace(entry.Greek)
            ? selection.Phrase
            : entry.Greek;
        StrongsEnglishText.Text = string.IsNullOrWhiteSpace(entry.English)
            ? selection.Phrase
            : entry.English;
        StrongsNumberText.Text = string.IsNullOrWhiteSpace(entry.DStrong)
            ? "Strong's: available when the full TAGNT dStrong row is loaded."
            : $"Strong's: {entry.DStrong}";
        StrongsGrammarText.Text = BuildTagntDetailLine(entry);

        var lexiconSummary = new[]
        {
            string.IsNullOrWhiteSpace(lexiconEntry?.Transliteration) ? null : $"Transliteration: {lexiconEntry.Transliteration}",
            string.IsNullOrWhiteSpace(lexiconEntry?.Morph) ? null : $"Morph: {lexiconEntry.Morph}",
            string.IsNullOrWhiteSpace(lexiconEntry?.Gloss) ? null : $"Gloss: {lexiconEntry.Gloss}",
            string.IsNullOrWhiteSpace(entry.DictionaryForm) ? null : $"TAGNT form: {entry.DictionaryForm}",
            string.IsNullOrWhiteSpace(entry.Gloss) ? null : $"TAGNT gloss: {entry.Gloss}",
            string.IsNullOrWhiteSpace(entry.SubMeaning) ? null : $"Context: {entry.SubMeaning}"
        }
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToList();
        StrongsLexiconSummaryText.Text = string.Join("\n", lexiconSummary);

        SetStrongsDefinitionDocument(lexiconEntry?.Meaning);
        UpdateStrongsSectionVisibility();

        if (StrongsPanelRoot.Visibility != Visibility.Visible)
        {
            StrongsPanelRoot.Visibility = Visibility.Visible;
            StrongsPanelRoot.Opacity = 0;
        }
        BringPanelToFront(MovablePanelKind.Strongs);

        var targetWidth = _strongsPanelVisibleWidth.Value <= 0
            ? 330
            : Math.Clamp(_strongsPanelVisibleWidth.Value, 1, Math.Max(1, StudyPanel.ActualWidth));
        AnimateStrongsPanelWidth(StrongsColumn.ActualWidth, targetWidth, show: true);
        Dispatcher.BeginInvoke(() =>
        {
            if (_currentStudy is not null)
            {
                ApplySavedPanelGeometry(MovablePanelKind.Strongs, _currentStudy.StrongsPanelGeometry);
                ClampAllPanelsToStudyArea(save: false);
            }
        }, DispatcherPriority.Loaded);
    }

    private void UpdateStrongsSectionVisibility()
    {
        StrongsWordSection.Visibility = HasVisibleText(StrongsGreekText) || HasVisibleText(StrongsEnglishText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsNumberSection.Visibility = HasVisibleText(StrongsNumberText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsTagntSection.Visibility = HasVisibleText(StrongsGrammarText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsLexiconSummarySection.Visibility = HasVisibleText(StrongsLexiconSummaryText)
            ? Visibility.Visible
            : Visibility.Collapsed;
        StrongsDefinitionSection.Visibility = StrongsDefinitionDocument.Blocks.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private static bool HasVisibleText(TextBlock textBlock)
    {
        return !string.IsNullOrWhiteSpace(textBlock.Text);
    }

    private void SetStrongsDefinitionDocument(string? meaning)
    {
        StrongsDefinitionDocument.Blocks.Clear();

        var definitionText = string.IsNullOrWhiteSpace(meaning)
            ? "Definition lookup is ready. Drop the TBESG Greek lexicon text file into the app Data folder to show the full Strong's definition here."
            : meaning;

        var lines = BuildDefinitionLines(definitionText);
        if (lines.Count == 0)
        {
            return;
        }

        var hasMainPoint = false;
        foreach (var line in lines)
        {
            if (line.Kind == DefinitionLineKind.Main)
            {
                if (hasMainPoint || StrongsDefinitionDocument.Blocks.Count > 0)
                {
                    AddDefinitionDivider();
                }

                hasMainPoint = true;
            }

            StrongsDefinitionDocument.Blocks.Add(CreateDefinitionParagraph(line));
        }

        ApplySavedRichTextAnnotations(StrongsDefinitionViewer);
    }

    private Paragraph CreateDefinitionParagraph(DefinitionLine line)
    {
        var paragraph = new Paragraph
        {
            Margin = line.Kind switch
            {
                DefinitionLineKind.Main => new Thickness(0, 0, 0, 8),
                DefinitionLineKind.Sub => new Thickness(34, 0, 0, 7),
                _ => new Thickness(0, 0, 0, 7)
            },
            LineHeight = line.Kind == DefinitionLineKind.Main ? 22 : 20,
            Foreground = new SolidColorBrush(Color.FromRgb(17, 17, 17))
        };

        if (!string.IsNullOrWhiteSpace(line.Marker))
        {
            paragraph.Inlines.Add(new Run(line.Marker)
            {
                FontSize = line.Kind == DefinitionLineKind.Main ? 18 : 15,
                FontWeight = FontWeights.Black,
                Foreground = GetResourceBrush(line.Kind == DefinitionLineKind.Main ? "PanelBackground" : "AppBackground")
            });
            paragraph.Inlines.Add(new Run(" "));
        }

        AddDefinitionTextRuns(paragraph, line.Text, line.Kind != DefinitionLineKind.Lead);
        return paragraph;
    }

    private void AddDefinitionDivider()
    {
        StrongsDefinitionDocument.Blocks.Add(new BlockUIContainer(new Border
        {
            Height = 1,
            Margin = new Thickness(0, 5, 0, 9),
            Background = GetResourceBrush("PanelBackground"),
            Opacity = 0.45
        }));
    }

    private void AddDefinitionTextRuns(Paragraph paragraph, string text, bool boldLeadPhrase)
    {
        var trimmed = text.Trim();
        if (!boldLeadPhrase)
        {
            AddDefinitionRunsWithReferences(paragraph, trimmed, 15, FontWeights.SemiBold);
            return;
        }

        var leadLength = FindDefinitionLeadPhraseLength(trimmed);
        if (leadLength <= 0)
        {
            AddDefinitionRunsWithReferences(paragraph, trimmed, 15, FontWeights.Normal);
            return;
        }

        AddDefinitionRunsWithReferences(paragraph, trimmed[..leadLength], paragraph.LineHeight >= 22 ? 16 : 15, FontWeights.Black);

        if (leadLength < trimmed.Length)
        {
            AddDefinitionRunsWithReferences(paragraph, trimmed[leadLength..], paragraph.LineHeight >= 22 ? 15 : 14, FontWeights.Normal);
        }
    }

    private void AddDefinitionRunsWithReferences(Paragraph paragraph, string text, double fontSize, FontWeight fontWeight)
    {
        const string referencePattern = @"(?<![A-Za-z0-9])(?<book>[1-3]?[A-Za-z]{2,4})\.(?<chapter>\d{1,3}):(?<verse>\d{1,3})(?![A-Za-z0-9])";
        var cursor = 0;
        foreach (Match match in Regex.Matches(text, referencePattern, RegexOptions.CultureInvariant))
        {
            if (match.Index > cursor)
            {
                paragraph.Inlines.Add(new Run(text[cursor..match.Index])
                {
                    FontSize = fontSize,
                    FontWeight = fontWeight
                });
            }

            if (TryCreateVerseReference(match, out var reference))
            {
                var link = new Span(new Run(reference.DisplayText))
                {
                    Cursor = Cursors.Hand,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Black,
                    Foreground = GetResourceBrush("PanelBackground"),
                    Tag = reference
                };
                link.MouseEnter += VerseReference_MouseEnter;
                link.MouseLeave += VerseReference_MouseLeave;
                link.MouseLeftButtonDown += VerseReference_MouseLeftButtonDown;
                paragraph.Inlines.Add(link);
            }
            else
            {
                paragraph.Inlines.Add(new Run(match.Value)
                {
                    FontSize = fontSize,
                    FontWeight = fontWeight
                });
            }

            cursor = match.Index + match.Length;
        }

        if (cursor < text.Length)
        {
            paragraph.Inlines.Add(new Run(text[cursor..])
            {
                FontSize = fontSize,
                FontWeight = fontWeight
            });
        }
    }

    private static bool TryCreateVerseReference(Match match, out VerseReference reference)
    {
        reference = new VerseReference(string.Empty, string.Empty, 0, 0);
        if (!TryMapTagntBookCode(match.Groups["book"].Value, out var bookName)
            || !int.TryParse(match.Groups["chapter"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var chapter)
            || !int.TryParse(match.Groups["verse"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var verse))
        {
            return false;
        }

        reference = new VerseReference(match.Value, bookName, chapter, verse);
        return true;
    }

    private static int FindDefinitionLeadPhraseLength(string text)
    {
        var delimiterIndex = text.IndexOfAny([',', ';', ':']);
        if (delimiterIndex < 0 || delimiterIndex > 72)
        {
            return 0;
        }

        return delimiterIndex + 1;
    }

    private static List<DefinitionLine> BuildDefinitionLines(string meaning)
    {
        var lines = new List<DefinitionLine>();
        if (string.IsNullOrWhiteSpace(meaning))
        {
            return lines;
        }

        var normalized = meaning.Trim();
        normalized = Regex.Replace(normalized, @"\s*__([IVXLCDM]+)\.\s*", "\n$1. ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\s*__([0-9]+)\.\s*", "\n$1. ", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\s*__\(([a-z])\)\s*", "\n$1. ", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"(?<!\n)(?<![A-Za-z])\s([0-9]+)\.\s+", "\n$1. ", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"(?<!\n)\s([a-z])\.\s+", "\n$1. ", RegexOptions.CultureInvariant);
        normalized = Regex.Replace(normalized, @"\n{3,}", "\n\n", RegexOptions.CultureInvariant);

        foreach (var rawLine in normalized.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries))
        {
            var line = Regex.Replace(rawLine.Trim(), @"\s+", " ", RegexOptions.CultureInvariant);
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var romanMatch = Regex.Match(line, @"^(?<marker>[IVXLCDM]+)\.\s*(?<text>.+)$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (romanMatch.Success)
            {
                lines.Add(new DefinitionLine(DefinitionLineKind.Main, "-", $"{romanMatch.Groups["marker"].Value}. {romanMatch.Groups["text"].Value.Trim()}"));
                continue;
            }

            var numberMatch = Regex.Match(line, @"^(?<marker>[0-9]+)\.\s*(?<text>.+)$", RegexOptions.CultureInvariant);
            if (numberMatch.Success)
            {
                lines.Add(new DefinitionLine(DefinitionLineKind.Main, "-", $"{numberMatch.Groups["marker"].Value}. {numberMatch.Groups["text"].Value.Trim()}"));
                continue;
            }

            var letterMatch = Regex.Match(line, @"^(?<marker>[a-z])\.\s*(?<text>.+)$", RegexOptions.CultureInvariant);
            if (letterMatch.Success)
            {
                lines.Add(new DefinitionLine(DefinitionLineKind.Sub, "↳", $"{letterMatch.Groups["marker"].Value}. {letterMatch.Groups["text"].Value.Trim()}"));
                continue;
            }

            lines.Add(new DefinitionLine(DefinitionLineKind.Lead, string.Empty, line));
        }

        return lines;
    }

    private static string BuildTagntDetailLine(TagntWordEntry entry)
    {
        var parts = new[]
        {
            string.IsNullOrWhiteSpace(entry.Grammar) ? null : $"Grammar: {entry.Grammar}",
            string.IsNullOrWhiteSpace(entry.WordType) ? null : $"Type: {entry.WordType}",
            string.IsNullOrWhiteSpace(entry.Editions) ? null : $"Editions: {entry.Editions}",
            string.IsNullOrWhiteSpace(entry.SimpleStrong) ? null : $"Simple Strong's: {entry.SimpleStrong}",
            string.IsNullOrWhiteSpace(entry.AltStrongs) ? null : $"Alt Strong's: {entry.AltStrongs}",
            string.IsNullOrWhiteSpace(entry.ConjoinedWord) ? null : $"Conjoined: {entry.ConjoinedWord}",
            string.IsNullOrWhiteSpace(entry.VariantNotes) ? null : $"Variants: {entry.VariantNotes}"
        }.Where(part => !string.IsNullOrWhiteSpace(part));

        var detail = string.Join("\n", parts);
        return string.IsNullOrWhiteSpace(detail)
            ? "Grammar and variant details will appear when those TAGNT rows are loaded."
            : detail;
    }

    private void PanelDragHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not UIElement handle || StudyPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        if (IsPanelHeaderControl(e.OriginalSource as DependencyObject))
        {
            return;
        }

        var kind = TryGetPanelKindFromElement(handle);
        if (kind is null || kind == MovablePanelKind.Strongs && StrongsPanelRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        _draggingPanelKind = kind;
        _draggingPanelHandle = handle;
        _draggingPanelTransform = GetPanelTransform(kind.Value);
        CommitTranslateTransformPosition(_draggingPanelTransform);
        _panelDragStartPoint = e.GetPosition(StudyPanel);
        _panelDragPointer = _panelDragStartPoint;
        _panelDragStartOffset = new Point(_draggingPanelTransform.X, _draggingPanelTransform.Y);
        _panelDragPendingOffset = _panelDragStartOffset;
        _hoveredPanelPreset = null;
        _hoveredPanelPresetCommand = null;
        var panel = GetPanelRoot(kind.Value);
        _panelDragOriginalOpacity = panel.Opacity;
        BringPanelToFront(kind.Value);
        panel.BeginAnimation(OpacityProperty, null);
        panel.Opacity = kind == MovablePanelKind.Editor ? _panelDragOriginalOpacity : 0.56;
        if (kind != MovablePanelKind.Editor)
        {
            ShowPanelPresetOverlay();
        }
        handle.CaptureMouse();
        Mouse.OverrideCursor = Cursors.SizeAll;
        e.Handled = true;
    }

    private static void CommitTranslateTransformPosition(TranslateTransform transform)
    {
        var currentX = transform.X;
        var currentY = transform.Y;
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        transform.X = currentX;
        transform.Y = currentY;
    }

    private void PanelDragHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_draggingPanelKind is null
            || _draggingPanelTransform is null
            || !ReferenceEquals(sender, _draggingPanelHandle)
            || e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        var pointer = e.GetPosition(StudyPanel);
        _panelDragPointer = pointer;
        var delta = pointer - _panelDragStartPoint;
        _panelDragPendingOffset = new Point(
            _panelDragStartOffset.X + delta.X,
            _panelDragStartOffset.Y + delta.Y);
        if (!_panelDragRenderSubscribed)
        {
            _panelDragRenderSubscribed = true;
            CompositionTarget.Rendering += PanelDrag_Rendering;
        }

        e.Handled = true;
    }

    private void PanelDrag_Rendering(object? sender, EventArgs e)
    {
        if (_draggingPanelKind is null || _draggingPanelTransform is null)
        {
            StopPanelDragRendering();
            return;
        }

        var clampedOffset = ClampPanelOffset(_draggingPanelKind.Value, _panelDragPendingOffset);
        _draggingPanelTransform.X = clampedOffset.X;
        _draggingPanelTransform.Y = clampedOffset.Y;
        if (_draggingPanelKind != MovablePanelKind.Editor)
        {
            UpdatePanelSnapBarForPointer(_panelDragPointer);
        }
        else if (SlashCommandMenu.Visibility == Visibility.Visible)
        {
            PositionSlashCommandMenu();
        }
    }

    private void PanelDragHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPanelKind is null || !ReferenceEquals(sender, _draggingPanelHandle))
        {
            return;
        }

        var kind = _draggingPanelKind.Value;
        var preset = _hoveredPanelPreset;
        var presetCommand = _hoveredPanelPresetCommand;
        StopPanelDragRendering();
        if (_draggingPanelHandle?.IsMouseCaptured == true)
        {
            _draggingPanelHandle.ReleaseMouseCapture();
        }

        Mouse.OverrideCursor = null;
        _draggingPanelKind = null;
        _draggingPanelHandle = null;
        _draggingPanelTransform = null;
        var panel = GetPanelRoot(kind);
        BringPanelToFront(kind);
        panel.BeginAnimation(OpacityProperty, new DoubleAnimation(panel.Opacity, _panelDragOriginalOpacity, TimeSpan.FromMilliseconds(140))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        if (kind != MovablePanelKind.Editor)
        {
            HidePanelPresetOverlay();
        }
        if (presetCommand == "PairSplit")
        {
            ApplyPanelPairPreset(split: true);
        }
        else if (presetCommand == "PairStack")
        {
            ApplyPanelPairPreset(split: false);
        }
        else if (preset is not null)
        {
            ApplyPanelPreset(kind, preset.Value, animate: true);
        }
        else
        {
            SaveCurrentPanelGeometry(kind, flush: true);
            UpdateEditorAvoidanceForPanels(animate: true);
        }
        e.Handled = true;
    }

    private MovablePanelKind? TryGetPanelKindFromElement(DependencyObject element)
    {
        var panel = FindOwningPanelRoot(element);
        return ReferenceEquals(panel, StudyEditorPanelRoot)
            ? MovablePanelKind.Editor
            : ReferenceEquals(panel, ScripturePanelRoot)
            || ReferenceEquals(element, ScripturePanelTopBar)
            || ReferenceEquals(element, ScripturePanelDragHandle)
                ? MovablePanelKind.Scripture
                : ReferenceEquals(panel, StrongsPanelRoot)
                  || ReferenceEquals(element, StrongsPanelTopBar)
                  || ReferenceEquals(element, StrongsPanelDragHandle)
                    ? MovablePanelKind.Strongs
                    : (MovablePanelKind?)null;
    }

    private Border? FindOwningPanelRoot(DependencyObject? element)
    {
        while (element is not null)
        {
            if (ReferenceEquals(element, ScripturePanelRoot))
            {
                return ScripturePanelRoot;
            }

            if (ReferenceEquals(element, StudyEditorPanelRoot))
            {
                return StudyEditorPanelRoot;
            }

            if (ReferenceEquals(element, StrongsPanelRoot))
            {
                return StrongsPanelRoot;
            }

            element = VisualTreeHelper.GetParent(element);
        }

        return null;
    }

    private static bool IsPanelHeaderControl(DependencyObject? source)
    {
        while (source is not null)
        {
            if (source is ButtonBase or Slider or TextBox or ComboBox or ScrollBar)
            {
                return true;
            }

            source = VisualTreeHelper.GetParent(source);
        }

        return false;
    }

    private void PanelRoot_PreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is DependencyObject source && TryGetPanelKindFromElement(source) is { } kind)
        {
            BringPanelToFront(kind);
        }
    }

    private void SnapshotCurrentStudyPanelGeometry()
    {
        if (_currentStudy is null || StudyPanel?.Visibility != Visibility.Visible)
        {
            return;
        }

        SnapshotPanelGeometry(MovablePanelKind.Editor);
        if (ScripturePanelRoot.Visibility == Visibility.Visible)
        {
            SnapshotPanelGeometry(MovablePanelKind.Scripture);
        }

        if (StrongsPanelRoot.Visibility == Visibility.Visible)
        {
            SnapshotPanelGeometry(MovablePanelKind.Strongs);
        }
    }

    private void SnapshotPanelGeometry(MovablePanelKind kind)
    {
        if (_currentStudy is not { } study)
        {
            return;
        }

        var panel = GetPanelRoot(kind);
        var bounds = GetPanelBounds(panel);
        if (bounds == Rect.Empty || bounds.Width <= 0 || bounds.Height <= 0)
        {
            return;
        }

        var geometry = new PanelGeometryState
        {
            X = Math.Round(bounds.Left, 2),
            Y = Math.Round(bounds.Top, 2),
            Width = Math.Round(bounds.Width, 2),
            Height = Math.Round(bounds.Height, 2)
        };

        switch (kind)
        {
            case MovablePanelKind.Editor:
                study.EditorPanelGeometry = geometry;
                break;
            case MovablePanelKind.Scripture:
                study.ScripturePanelGeometry = geometry;
                break;
            case MovablePanelKind.Strongs:
                study.StrongsPanelGeometry = geometry;
                break;
        }
    }

    private void BringPanelToFront(MovablePanelKind kind)
    {
        Panel.SetZIndex(GetPanelRoot(kind), ++_topPanelZIndex);
    }

    private void StopPanelDragRendering()
    {
        if (_panelDragRenderSubscribed)
        {
            CompositionTarget.Rendering -= PanelDrag_Rendering;
            _panelDragRenderSubscribed = false;
        }
    }

    private void ShowPanelPresetOverlay()
    {
        if (PanelPresetOverlay is null)
        {
            return;
        }

        PanelPresetOverlay.Visibility = Visibility.Visible;
        PanelPresetOverlay.IsHitTestVisible = false;
        PanelPresetTray.Visibility = Visibility.Collapsed;
        _panelPresetTrayOpen = false;
        PositionPanelPresetTargets();
        AnimatePanelSnapBar(open: false);
        SetPanelPresetHighlight(null);
        HidePanelPresetPreviews();
    }

    private void HidePanelPresetOverlay()
    {
        if (PanelPresetOverlay is null)
        {
            return;
        }

        PanelPresetOverlay.Visibility = Visibility.Collapsed;
        PanelPresetOverlay.IsHitTestVisible = false;
        _hoveredPanelPreset = null;
        _hoveredPanelPresetCommand = null;
        _panelPresetTrayOpen = false;
        PanelPresetTray.Visibility = Visibility.Collapsed;
        HidePanelPresetPreviews();
        foreach (var target in new[] { PanelPresetTop, PanelPresetLeft, PanelPresetRight, PanelPresetBottom, PanelPresetFloat })
        {
            target.Background = PanelPresetIdleBrush;
            target.BorderBrush = PanelPresetIdleBorderBrush;
        }
        foreach (var target in new[] { PanelPresetBothSplit, PanelPresetBothStack })
        {
            target.Background = PanelPresetIdleBrush;
            target.BorderBrush = PanelPresetIdleBorderBrush;
            target.Opacity = 0.84;
        }
    }

    private void PositionPanelPresetTargets()
    {
        var width = Math.Max(360, StudyPanel.ActualWidth);
        var barWidth = StrongsPanelRoot.Visibility == Visibility.Visible ? 760 : 600;
        PanelSnapBar.Width = Math.Min(barWidth, Math.Max(320, width - 28));
        Canvas.SetLeft(PanelSnapBar, Math.Max(14, (width - PanelSnapBar.Width) / 2));
        Canvas.SetTop(PanelSnapBar, 0);
        var showPairPresets = StrongsPanelRoot.Visibility == Visibility.Visible && ScripturePanelRoot.Visibility == Visibility.Visible;
        PanelPresetBothSplit.Visibility = showPairPresets ? Visibility.Visible : Visibility.Collapsed;
        PanelPresetBothStack.Visibility = showPairPresets ? Visibility.Visible : Visibility.Collapsed;
    }

    private void PanelPreset_MouseEnter(object sender, MouseEventArgs e)
    {
        if (sender is Border { Tag: string tag } && Enum.TryParse<PanelPreset>(tag, out var preset))
        {
            _hoveredPanelPreset = preset;
            _hoveredPanelPresetCommand = null;
            SetPanelPresetHighlight(preset);
            ShowPanelPresetPreview(preset);
        }
        else if (sender is Border { Tag: string command })
        {
            _hoveredPanelPreset = null;
            _hoveredPanelPresetCommand = command;
            SetPanelPresetCommandHighlight(command);
            ShowPanelPresetCommandPreview(command);
        }
    }

    private void PanelPreset_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_draggingPanelKind is not null)
        {
            SetPanelPresetHighlight(_hoveredPanelPreset);
        }
    }

    private void PanelPreset_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_draggingPanelKind is null || sender is not Border { Tag: string tag })
        {
            return;
        }

        if (Enum.TryParse<PanelPreset>(tag, out var preset))
        {
            _hoveredPanelPreset = preset;
            _hoveredPanelPresetCommand = null;
        }
        else
        {
            _hoveredPanelPreset = null;
            _hoveredPanelPresetCommand = tag;
        }

        PanelDragHandle_MouseLeftButtonUp(_draggingPanelHandle!, e);
        e.Handled = true;
    }

    private void SetPanelPresetHighlight(PanelPreset? preset)
    {
        foreach (var target in new[] { PanelPresetTop, PanelPresetLeft, PanelPresetRight, PanelPresetBottom, PanelPresetFloat })
        {
            var isSelected = preset.HasValue
                && string.Equals(target.Tag as string, preset.Value.ToString(), StringComparison.OrdinalIgnoreCase);
            target.Background = isSelected ? PanelPresetActiveBrush : PanelPresetIdleBrush;
            target.BorderBrush = isSelected ? PanelPresetActiveBorderBrush : PanelPresetIdleBorderBrush;
            target.Opacity = isSelected ? 1 : 0.84;
        }

        if (preset is not null)
        {
            _hoveredPanelPresetCommand = null;
            ShowPanelPresetPreview(preset.Value);
        }
        else
        {
            HidePanelPresetPreviews();
        }

        foreach (var target in new[] { PanelPresetBothSplit, PanelPresetBothStack })
        {
            target.Background = PanelPresetIdleBrush;
            target.BorderBrush = PanelPresetIdleBorderBrush;
            target.Opacity = 0.84;
        }
    }

    private void SetPanelPresetCommandHighlight(string? command)
    {
        SetPanelPresetHighlight(null);
        foreach (var target in new[] { PanelPresetBothSplit, PanelPresetBothStack })
        {
            var isSelected = string.Equals(target.Tag as string, command, StringComparison.OrdinalIgnoreCase);
            target.Background = isSelected ? PanelPresetActiveBrush : PanelPresetIdleBrush;
            target.BorderBrush = isSelected ? PanelPresetActiveBorderBrush : PanelPresetIdleBorderBrush;
            target.Opacity = isSelected ? 1 : 0.84;
        }

        if (command is not null)
        {
            ShowPanelPresetCommandPreview(command);
        }
    }

    private PanelPreset GetNearestPanelPreset()
    {
        if (_draggingPanelKind is null)
        {
            return PanelPreset.Right;
        }

        var width = Math.Max(1, StudyPanel.ActualWidth);
        var height = Math.Max(1, StudyPanel.ActualHeight);
        var pointer = _panelDragPointer;
        if (pointer.Y < height * 0.20)
        {
            return PanelPreset.Top;
        }

        if (pointer.Y > height * 0.80)
        {
            return PanelPreset.Bottom;
        }

        if (pointer.X < width * 0.20)
        {
            return PanelPreset.Left;
        }

        if (pointer.X > width * 0.80)
        {
            return PanelPreset.Right;
        }

        return PanelPreset.Float;
    }

    private Point ClampPanelOffset(MovablePanelKind kind, Point candidateOffset)
    {
        var panel = GetPanelRoot(kind);
        var transform = GetPanelTransform(kind);
        if (panel.ActualWidth <= 0 || panel.ActualHeight <= 0 || StudyPanel.ActualWidth <= 0 || StudyPanel.ActualHeight <= 0)
        {
            return candidateOffset;
        }

        var bounds = GetPanelBounds(panel);
        if (bounds == Rect.Empty)
        {
            return candidateOffset;
        }

        var layoutLeft = bounds.Left - transform.X;
        var layoutTop = bounds.Top - transform.Y;
        var minX = -layoutLeft;
        var minY = -layoutTop;
        var maxX = Math.Max(minX, StudyPanel.ActualWidth - layoutLeft - panel.ActualWidth);
        var maxY = Math.Max(minY, StudyPanel.ActualHeight - layoutTop - panel.ActualHeight);
        return new Point(
            Math.Clamp(candidateOffset.X, minX, maxX),
            Math.Clamp(candidateOffset.Y, minY, maxY));
    }

    private void UpdatePanelSnapBarForPointer(Point pointer)
    {
        const double openTrigger = 22;
        if (pointer.Y <= openTrigger)
        {
            if (!_panelPresetTrayOpen)
            {
                _panelPresetTrayOpen = true;
                PanelPresetTray.Visibility = Visibility.Visible;
                AnimatePanelSnapBar(open: true);
            }

            UpdatePresetUnderTopBarPointer(pointer);
            return;
        }

        if (_panelPresetTrayOpen)
        {
            var trayBounds = GetSnapBarBounds();
            if (trayBounds.Contains(pointer))
            {
                UpdatePresetUnderTopBarPointer(pointer);
                return;
            }
        }

        if (_panelPresetTrayOpen)
        {
            _panelPresetTrayOpen = false;
            PanelPresetTray.Visibility = Visibility.Collapsed;
            _hoveredPanelPreset = null;
            _hoveredPanelPresetCommand = null;
            SetPanelPresetHighlight(null);
            HidePanelPresetPreviews();
            AnimatePanelSnapBar(open: false);
        }
    }

    private void AnimatePanelSnapBar(bool open)
    {
        PanelSnapBarTransform.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(
            PanelSnapBarTransform.Y,
            open ? 0 : -58,
            TimeSpan.FromMilliseconds(open ? 210 : 160))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private Rect GetSnapBarBounds()
    {
        if (PanelSnapBar.ActualWidth <= 0 || PanelSnapBar.ActualHeight <= 0)
        {
            return Rect.Empty;
        }

        var topLeft = PanelSnapBar.TransformToAncestor(StudyPanel).Transform(new Point(0, 0));
        var padding = _panelPresetTrayOpen ? 18 : 4;
        return new Rect(
            topLeft.X - padding,
            topLeft.Y - padding,
            PanelSnapBar.ActualWidth + padding * 2,
            PanelSnapBar.ActualHeight + padding * 2);
    }

    private void UpdatePresetUnderTopBarPointer(Point pointer)
    {
        var presetElements = new[]
        {
            PanelPresetTop,
            PanelPresetLeft,
            PanelPresetRight,
            PanelPresetBottom,
            PanelPresetFloat,
            PanelPresetBothSplit,
            PanelPresetBothStack
        };

        foreach (var element in presetElements)
        {
            if (element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
            {
                continue;
            }

            var topLeft = element.TransformToAncestor(StudyPanel).Transform(new Point(0, 0));
            var bounds = new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
            if (!bounds.Contains(pointer) || element.Tag is not string tag)
            {
                continue;
            }

            if (Enum.TryParse<PanelPreset>(tag, out var preset))
            {
                _hoveredPanelPreset = preset;
                _hoveredPanelPresetCommand = null;
                SetPanelPresetHighlight(preset);
            }
            else
            {
                _hoveredPanelPreset = null;
                _hoveredPanelPresetCommand = tag;
                SetPanelPresetCommandHighlight(tag);
            }

            return;
        }

        _hoveredPanelPreset = null;
        _hoveredPanelPresetCommand = null;
        SetPanelPresetHighlight(null);
    }

    private void ShowPanelPresetPreview(PanelPreset preset)
    {
        if (_draggingPanelKind is null)
        {
            HidePanelPresetPreviews();
            return;
        }

        var bounds = GetPreviewBounds(_draggingPanelKind.Value, preset);
        AnimatePanelPresetPreview(PanelPresetPreviewPrimary, bounds, 0.22);
        AnimatePanelPresetPreview(PanelPresetPreviewSecondary, null, 0);
    }

    private void ShowPanelPresetCommandPreview(string command)
    {
        if (string.Equals(command, "PairSplit", StringComparison.OrdinalIgnoreCase)
            && ScripturePanelRoot.Visibility == Visibility.Visible
            && StrongsPanelRoot.Visibility == Visibility.Visible)
        {
            AnimatePanelPresetPreview(PanelPresetPreviewPrimary, GetPreviewBounds(MovablePanelKind.Scripture, PanelPreset.Left), 0.22);
            AnimatePanelPresetPreview(PanelPresetPreviewSecondary, GetPreviewBounds(MovablePanelKind.Strongs, PanelPreset.Right), 0.20);
            return;
        }

        if (string.Equals(command, "PairStack", StringComparison.OrdinalIgnoreCase)
            && ScripturePanelRoot.Visibility == Visibility.Visible
            && StrongsPanelRoot.Visibility == Visibility.Visible)
        {
            AnimatePanelPresetPreview(PanelPresetPreviewPrimary, GetPreviewBounds(MovablePanelKind.Scripture, PanelPreset.Top), 0.22);
            AnimatePanelPresetPreview(PanelPresetPreviewSecondary, GetPreviewBounds(MovablePanelKind.Strongs, PanelPreset.Bottom), 0.20);
            return;
        }

        HidePanelPresetPreviews();
    }

    private Rect GetPreviewBounds(MovablePanelKind kind, PanelPreset preset)
    {
        var studyWidth = Math.Max(1, StudyPanel.ActualWidth);
        var studyHeight = Math.Max(1, StudyPanel.ActualHeight);
        var panel = GetPanelRoot(kind);
        var width = Math.Clamp(panel.ActualWidth > 0 ? panel.ActualWidth : kind == MovablePanelKind.Scripture ? 440 : 330,
            1,
            Math.Max(1, studyWidth - 28));
        var height = panel.ActualHeight > 0 ? panel.ActualHeight : Math.Max(1, studyHeight * 0.58);

        switch (preset)
        {
            case PanelPreset.Top:
            case PanelPreset.Bottom:
                height = Math.Max(1, studyHeight * 0.46);
                break;
            case PanelPreset.Left:
            case PanelPreset.Right:
                height = studyHeight;
                break;
            case PanelPreset.Float:
                height = Math.Max(1, studyHeight * 0.58);
                break;
        }

        var topLeft = GetPresetTopLeft(preset, new Rect(0, 0, width, height));
        return new Rect(topLeft, new Size(width, Math.Min(height, studyHeight)));
    }

    private void AnimatePanelPresetPreview(Border preview, Rect? target, double opacity)
    {
        var duration = new Duration(TimeSpan.FromMilliseconds(170));
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        if (target is not { } rect || rect.Width <= 0 || rect.Height <= 0)
        {
            preview.BeginAnimation(OpacityProperty, new DoubleAnimation(preview.Opacity, 0, duration)
            {
                EasingFunction = ease
            });
            return;
        }

        preview.Visibility = Visibility.Visible;
        AnimateCanvasMetric(preview, Canvas.LeftProperty, rect.Left, duration, ease);
        AnimateCanvasMetric(preview, Canvas.TopProperty, rect.Top, duration, ease);
        preview.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(preview.ActualWidth > 1 ? preview.ActualWidth : rect.Width, rect.Width, duration)
        {
            EasingFunction = ease
        });
        preview.BeginAnimation(FrameworkElement.HeightProperty, new DoubleAnimation(preview.ActualHeight > 1 ? preview.ActualHeight : rect.Height, rect.Height, duration)
        {
            EasingFunction = ease
        });
        preview.BeginAnimation(OpacityProperty, new DoubleAnimation(preview.Opacity, opacity, duration)
        {
            EasingFunction = ease
        });
    }

    private static void AnimateCanvasMetric(UIElement target, DependencyProperty property, double to, Duration duration, IEasingFunction ease)
    {
        var current = (double)target.GetValue(property);
        if (double.IsNaN(current))
        {
            current = to;
        }

        target.BeginAnimation(property, new DoubleAnimation(current, to, duration)
        {
            EasingFunction = ease
        });
    }

    private void HidePanelPresetPreviews()
    {
        AnimatePanelPresetPreview(PanelPresetPreviewPrimary, null, 0);
        AnimatePanelPresetPreview(PanelPresetPreviewSecondary, null, 0);
    }

    private Border GetPanelRoot(MovablePanelKind kind)
    {
        return kind switch
        {
            MovablePanelKind.Editor => StudyEditorPanelRoot,
            MovablePanelKind.Scripture => ScripturePanelRoot,
            _ => StrongsPanelRoot
        };
    }

    private TranslateTransform GetPanelTransform(MovablePanelKind kind)
    {
        return kind switch
        {
            MovablePanelKind.Editor => StudyEditorPanelDragTransform,
            MovablePanelKind.Scripture => ScripturePanelDragTransform,
            _ => StrongsPanelDragTransform
        };
    }

    private Rect GetPanelBounds(Border panel)
    {
        if (panel.ActualWidth <= 0 || panel.ActualHeight <= 0 || StudyPanel.ActualWidth <= 0)
        {
            return Rect.Empty;
        }

        var transform = panel.TransformToAncestor(StudyPanel);
        var topLeft = transform.Transform(new Point(0, 0));
        var bottomRight = transform.Transform(new Point(panel.ActualWidth, panel.ActualHeight));
        return new Rect(topLeft, bottomRight);
    }

    private void ApplyPanelPreset(MovablePanelKind kind, PanelPreset preset, bool animate)
    {
        var panel = GetPanelRoot(kind);
        var transform = GetPanelTransform(kind);
        if (panel.ActualWidth <= 0 || panel.ActualHeight <= 0)
        {
            return;
        }

        ConfigurePanelLayoutForPreset(panel, preset);
        StudyPanel.UpdateLayout();
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);
        var currentBounds = GetPanelBounds(panel);
        var desired = GetPresetTopLeft(preset, currentBounds);
        var targetX = transform.X + desired.X - currentBounds.Left;
        var targetY = transform.Y + desired.Y - currentBounds.Top;
        if (animate)
        {
            var duration = new Duration(TimeSpan.FromMilliseconds(230));
            var ease = new QuarticEase { EasingMode = EasingMode.EaseOut };
            transform.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(transform.X, targetX, duration)
            {
                EasingFunction = ease
            });
            var yAnimation = new DoubleAnimation(transform.Y, targetY, duration)
            {
                EasingFunction = ease
            };
            yAnimation.Completed += (_, _) =>
            {
                transform.BeginAnimation(TranslateTransform.XProperty, null);
                transform.BeginAnimation(TranslateTransform.YProperty, null);
                transform.X = targetX;
                transform.Y = targetY;
                UpdateEditorAvoidanceForPanels(animate: false);
                SaveCurrentPanelGeometry(kind, flush: true);
            };
            transform.BeginAnimation(TranslateTransform.YProperty, yAnimation);
        }
        else
        {
            transform.X = targetX;
            transform.Y = targetY;
            SaveCurrentPanelGeometry(kind, flush: true);
        }

        if (kind == MovablePanelKind.Scripture)
        {
            _scripturePanelPreset = preset;
        }
        else
        {
            _strongsPanelPreset = preset;
        }

        Dispatcher.BeginInvoke(() => UpdateEditorAvoidanceForPanels(animate), DispatcherPriority.Render);
    }

    private Point GetPresetTopLeft(PanelPreset preset, Rect currentBounds)
    {
        var width = Math.Max(1, StudyPanel.ActualWidth);
        var height = Math.Max(1, StudyPanel.ActualHeight);
        const double edge = 14;
        return preset switch
        {
            PanelPreset.Top => new Point(Math.Max(edge, (width - currentBounds.Width) / 2), edge),
            PanelPreset.Left => new Point(edge, 0),
            PanelPreset.Bottom => new Point(Math.Max(edge, (width - currentBounds.Width) / 2), Math.Max(edge, height - currentBounds.Height - edge)),
            PanelPreset.Float => new Point(Math.Max(edge, (width - currentBounds.Width) / 2), Math.Max(edge, (height - currentBounds.Height) / 2)),
            _ => new Point(Math.Max(edge, width - currentBounds.Width - edge), 0)
        };
    }

    private void ConfigurePanelLayoutForPreset(Border panel, PanelPreset preset)
    {
        var height = Math.Max(1, StudyPanel.ActualHeight);
        panel.VerticalAlignment = VerticalAlignment.Top;
        switch (preset)
        {
            case PanelPreset.Top:
            case PanelPreset.Bottom:
                panel.Height = Math.Max(1, height * 0.46);
                break;
            case PanelPreset.Float:
                panel.Height = Math.Max(1, height * 0.58);
                break;
            default:
                panel.Height = height;
                break;
        }
    }

    private void ApplyPanelPairPreset(bool split)
    {
        if (StrongsPanelRoot.Visibility != Visibility.Visible || ScripturePanelRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        if (split)
        {
            ApplyPanelPreset(MovablePanelKind.Scripture, PanelPreset.Left, animate: true);
            ApplyPanelPreset(MovablePanelKind.Strongs, PanelPreset.Right, animate: true);
            return;
        }

        ApplyPanelPreset(MovablePanelKind.Scripture, PanelPreset.Top, animate: true);
        ApplyPanelPreset(MovablePanelKind.Strongs, PanelPreset.Bottom, animate: true);
    }

    private void ApplyStudyPanelGeometry(WorkspaceItem study)
    {
        if (!ReferenceEquals(_currentStudy, study) || StudyPanel.Visibility != Visibility.Visible)
        {
            return;
        }

        if (ShouldResetStudyPanelGeometry(study))
        {
            ApplyDefaultStudyPanelGeometry(study, save: true);
            UpdateEditorAvoidanceForPanels(animate: false);
            return;
        }

        ApplySavedPanelGeometry(MovablePanelKind.Editor, study.EditorPanelGeometry);

        if (ScripturePanelRoot.Visibility == Visibility.Visible)
        {
            ApplySavedPanelGeometry(MovablePanelKind.Scripture, study.ScripturePanelGeometry);
        }

        if (StrongsPanelRoot.Visibility == Visibility.Visible)
        {
            ApplySavedPanelGeometry(MovablePanelKind.Strongs, study.StrongsPanelGeometry);
        }
        ClampAllPanelsToStudyArea(save: false);
        UpdateEditorAvoidanceForPanels(animate: false);
    }

    private bool ShouldResetStudyPanelGeometry(WorkspaceItem study)
    {
        if (study.EditorPanelGeometry is null || study.ScripturePanelGeometry is null)
        {
            return true;
        }

        return !IsValidSavedPanelGeometry(study.EditorPanelGeometry)
               || !IsValidSavedPanelGeometry(study.ScripturePanelGeometry)
               || study.StrongsPanelGeometry is not null && !IsValidSavedPanelGeometry(study.StrongsPanelGeometry);
    }

    private static bool IsValidSavedPanelGeometry(PanelGeometryState geometry)
    {
        return double.IsFinite(geometry.X)
               && double.IsFinite(geometry.Y)
               && double.IsFinite(geometry.Width)
               && double.IsFinite(geometry.Height)
               && geometry.Width > 0
               && geometry.Height > 0;
    }

    private void ApplyDefaultStudyPanelGeometry(WorkspaceItem study, bool save)
    {
        var width = Math.Max(1, StudyPanel.ActualWidth);
        var height = Math.Max(1, StudyPanel.ActualHeight);
        var editorWidth = Math.Max(1, Math.Floor(width * 0.5));
        var scriptureWidth = Math.Max(1, width - editorWidth);

        study.EditorPanelGeometry = new PanelGeometryState
        {
            X = 0,
            Y = 0,
            Width = editorWidth,
            Height = height
        };
        study.ScripturePanelGeometry = new PanelGeometryState
        {
            X = editorWidth,
            Y = 0,
            Width = scriptureWidth,
            Height = height
        };

        ApplySavedPanelGeometry(MovablePanelKind.Editor, study.EditorPanelGeometry);
        if (ScripturePanelRoot.Visibility == Visibility.Visible)
        {
            ApplySavedPanelGeometry(MovablePanelKind.Scripture, study.ScripturePanelGeometry);
        }

        if (StrongsPanelRoot.Visibility == Visibility.Visible)
        {
            ApplySavedPanelGeometry(MovablePanelKind.Strongs, study.StrongsPanelGeometry);
        }

        ClampAllPanelsToStudyArea(save: false);
        if (save)
        {
            SaveWorkspaceState();
        }
    }

    private void ApplySavedPanelGeometry(MovablePanelKind kind, PanelGeometryState? geometry)
    {
        var panel = GetPanelRoot(kind);
        var transform = GetPanelTransform(kind);
        transform.BeginAnimation(TranslateTransform.XProperty, null);
        transform.BeginAnimation(TranslateTransform.YProperty, null);

        if (geometry is null || geometry.Width <= 0 || geometry.Height <= 0)
        {
            panel.BeginAnimation(FrameworkElement.WidthProperty, null);
            panel.BeginAnimation(FrameworkElement.HeightProperty, null);
            var defaultWidth = kind switch
            {
                MovablePanelKind.Editor => Math.Max(1, StudyPanel.ActualWidth * 0.5),
                MovablePanelKind.Scripture => Math.Max(1, StudyPanel.ActualWidth * 0.5),
                _ => 330
            };
            var defaultHeight = kind == MovablePanelKind.Editor
                ? Math.Max(1, StudyPanel.ActualHeight)
                : kind == MovablePanelKind.Scripture
                    ? Math.Max(1, StudyPanel.ActualHeight)
                    : Math.Max(1, StudyPanel.ActualHeight * 0.72);
            SetPanelSize(kind, defaultWidth, defaultHeight);
            panel.VerticalAlignment = VerticalAlignment.Top;
            var preset = kind == MovablePanelKind.Editor
                ? PanelPreset.Left
                : kind == MovablePanelKind.Scripture
                    ? PanelPreset.Right
                    : PanelPreset.Float;
            var desired = GetPresetTopLeft(preset, new Rect(0, 0, panel.Width, panel.Height));
            transform.X = desired.X;
            transform.Y = desired.Y;
            return;
        }

        SetPanelSize(kind, geometry.Width, geometry.Height);
        panel.VerticalAlignment = VerticalAlignment.Top;
        StudyPanel.UpdateLayout();
        var currentBounds = GetPanelBounds(panel);
        if (currentBounds == Rect.Empty)
        {
            return;
        }

        var transformOffset = new Point(
            transform.X + geometry.X - currentBounds.Left,
            transform.Y + geometry.Y - currentBounds.Top);
        var clampedOffset = ClampPanelOffset(kind, transformOffset);
        transform.X = clampedOffset.X;
        transform.Y = clampedOffset.Y;
    }

    private void SaveCurrentPanelGeometry(MovablePanelKind kind, bool flush = false)
    {
        var panel = GetPanelRoot(kind);
        var bounds = GetPanelBounds(panel);
        if (bounds == Rect.Empty)
        {
            return;
        }

        SavePanelGeometry(kind, bounds.Left, bounds.Top, bounds.Width, bounds.Height, flush);
    }

    private void SavePanelGeometry(MovablePanelKind kind, double x, double y, double width, double height, bool flush = false)
    {
        if (_currentStudy is null || width <= 0 || height <= 0)
        {
            return;
        }

        var geometry = new PanelGeometryState
        {
            X = Math.Round(x, 2),
            Y = Math.Round(y, 2),
            Width = Math.Round(width, 2),
            Height = Math.Round(height, 2)
        };

        if (kind == MovablePanelKind.Scripture)
        {
            _currentStudy.ScripturePanelGeometry = geometry;
        }
        else if (kind == MovablePanelKind.Editor)
        {
            _currentStudy.EditorPanelGeometry = geometry;
        }
        else
        {
            _currentStudy.StrongsPanelGeometry = geometry;
        }

        if (flush)
        {
            SaveWorkspaceState();
        }
        else
        {
            QueueWorkspaceSave();
        }
    }

    private void SetPanelSize(MovablePanelKind kind, double width, double height, bool updateColumns = false)
    {
        var panel = GetPanelRoot(kind);
        var maxWidth = Math.Max(1, StudyPanel.ActualWidth);
        var maxHeight = Math.Max(1, StudyPanel.ActualHeight);
        var safeWidth = Math.Round(Math.Clamp(width, 1, maxWidth));
        var safeHeight = Math.Round(Math.Clamp(height, 1, maxHeight));

        panel.BeginAnimation(FrameworkElement.WidthProperty, null);
        panel.BeginAnimation(FrameworkElement.HeightProperty, null);
        panel.Width = safeWidth;
        panel.Height = safeHeight;
        panel.VerticalAlignment = VerticalAlignment.Top;

        if (!updateColumns)
        {
            return;
        }

        if (kind == MovablePanelKind.Scripture)
        {
            ScriptureColumn.Width = new GridLength(safeWidth);
            _scripturePanelVisibleWidth = new GridLength(safeWidth);
        }
        else
        {
            StrongsColumn.Width = new GridLength(safeWidth);
            _strongsPanelVisibleWidth = new GridLength(safeWidth);
        }
    }

    private void PanelResizeThumb_DragStarted(object sender, DragStartedEventArgs e)
    {
        if (sender is not Thumb thumb
            || FindAncestor<Border>(thumb) is not { } panel
            || StudyPanel.ActualWidth <= 0
            || StudyPanel.ActualHeight <= 0)
        {
            return;
        }

        var kind = GetPanelKindFromRoot(panel);
        if (kind is null)
        {
            return;
        }

        BringPanelToFront(kind.Value);
        var transform = GetPanelTransform(kind.Value);
        CommitTranslateTransformPosition(transform);

        var bounds = GetPanelBounds(panel);
        if (bounds == Rect.Empty)
        {
            return;
        }

        _resizingPanelKind = kind;
        _panelResizeStartBounds = bounds;
        _panelResizeStartPointer = Mouse.GetPosition(StudyPanel);
        _panelResizePendingBounds = bounds;
        _panelResizeOriginalCacheMode = panel.CacheMode;
        panel.CacheMode = null;
        _panelResizeAccumulatedDelta = new Vector();
    }

    private void PanelResizeThumb_DragDelta(object sender, DragDeltaEventArgs e)
    {
        if (sender is not Thumb { Tag: string tag } thumb
            || FindAncestor<Border>(thumb) is not { } panel
            || StudyPanel.ActualWidth <= 0
            || StudyPanel.ActualHeight <= 0)
        {
            return;
        }

        var kind = GetPanelKindFromRoot(panel);
        if (kind is null)
        {
            return;
        }

        if (_resizingPanelKind != kind || _panelResizeStartBounds == Rect.Empty)
        {
            PanelResizeThumb_DragStarted(sender, new DragStartedEventArgs(0, 0));
        }

        var resizeLeft = tag.Contains("Left", StringComparison.OrdinalIgnoreCase);
        var resizeRight = tag.Contains("Right", StringComparison.OrdinalIgnoreCase);
        var resizeTop = tag.Contains("Top", StringComparison.OrdinalIgnoreCase);
        var resizeBottom = tag.Contains("Bottom", StringComparison.OrdinalIgnoreCase);
        const double minWidth = 1;
        const double minHeight = 1;

        var pointer = Mouse.GetPosition(StudyPanel);
        _panelResizeAccumulatedDelta = pointer - _panelResizeStartPointer;
        var left = _panelResizeStartBounds.Left;
        var right = _panelResizeStartBounds.Right;
        var top = _panelResizeStartBounds.Top;
        var bottom = _panelResizeStartBounds.Bottom;

        if (resizeLeft)
        {
            left = Math.Clamp(left + _panelResizeAccumulatedDelta.X, 0, right - minWidth);
        }

        if (resizeRight)
        {
            right = Math.Clamp(right + _panelResizeAccumulatedDelta.X, left + minWidth, StudyPanel.ActualWidth);
        }

        if (resizeTop)
        {
            top = Math.Clamp(top + _panelResizeAccumulatedDelta.Y, 0, bottom - minHeight);
        }

        if (resizeBottom)
        {
            bottom = Math.Clamp(bottom + _panelResizeAccumulatedDelta.Y, top + minHeight, StudyPanel.ActualHeight);
        }

        left = Math.Round(left);
        top = Math.Round(top);
        right = Math.Round(Math.Max(left + minWidth, right));
        bottom = Math.Round(Math.Max(top + minHeight, bottom));

        _resizingPanelKind = kind;
        _panelResizePendingBounds = new Rect(new Point(left, top), new Point(right, bottom));
        if (!_panelResizeRenderSubscribed)
        {
            _panelResizeRenderSubscribed = true;
            CompositionTarget.Rendering += PanelResize_Rendering;
        }
    }

    private void PanelResize_Rendering(object? sender, EventArgs e)
    {
        if (_resizingPanelKind is null)
        {
            StopPanelResizeRendering();
            return;
        }

        ApplyPendingPanelResize(updateAvoidance: false);
    }

    private void ApplyPendingPanelResize(bool updateAvoidance)
    {
        if (_resizingPanelKind is null || _panelResizePendingBounds == Rect.Empty)
        {
            return;
        }

        var kind = _resizingPanelKind.Value;
        var panel = GetPanelRoot(kind);
        var bounds = GetPanelBounds(panel);
        if (bounds == Rect.Empty)
        {
            return;
        }

        var transform = GetPanelTransform(kind);
        SetPanelSize(kind, _panelResizePendingBounds.Width, _panelResizePendingBounds.Height);
        transform.X += _panelResizePendingBounds.Left - bounds.Left;
        transform.Y += _panelResizePendingBounds.Top - bounds.Top;
        var clampedOffset = ClampPanelOffset(kind, new Point(transform.X, transform.Y));
        transform.X = Math.Round(clampedOffset.X);
        transform.Y = Math.Round(clampedOffset.Y);

        _ = updateAvoidance;
    }

    private void StopPanelResizeRendering()
    {
        if (_panelResizeRenderSubscribed)
        {
            CompositionTarget.Rendering -= PanelResize_Rendering;
            _panelResizeRenderSubscribed = false;
        }
    }

    private void PanelResizeThumb_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (sender is not Thumb thumb || FindAncestor<Border>(thumb) is not { } panel)
        {
            return;
        }

        var kind = GetPanelKindFromRoot(panel);
        if (kind is null)
        {
            return;
        }

        ApplyPendingPanelResize(updateAvoidance: false);
        StopPanelResizeRendering();
        panel.CacheMode = _panelResizeOriginalCacheMode;
        _panelResizeOriginalCacheMode = null;
        _resizingPanelKind = null;
        _panelResizeStartBounds = Rect.Empty;
        _panelResizeStartPointer = new Point();
        _panelResizePendingBounds = Rect.Empty;
        _panelResizeAccumulatedDelta = new Vector();
        BringPanelToFront(kind.Value);
        SaveCurrentPanelGeometry(kind.Value, flush: true);
    }

    private MovablePanelKind? GetPanelKindFromRoot(Border panel)
    {
        return ReferenceEquals(panel, StudyEditorPanelRoot)
            ? MovablePanelKind.Editor
            : ReferenceEquals(panel, ScripturePanelRoot)
                ? MovablePanelKind.Scripture
                : ReferenceEquals(panel, StrongsPanelRoot)
                    ? MovablePanelKind.Strongs
                    : (MovablePanelKind?)null;
    }

    private void ClampAllPanelsToStudyArea(bool save)
    {
        foreach (var kind in new[] { MovablePanelKind.Editor, MovablePanelKind.Scripture, MovablePanelKind.Strongs })
        {
            var panel = GetPanelRoot(kind);
            if (panel.Visibility != Visibility.Visible)
            {
                continue;
            }

            var transform = GetPanelTransform(kind);
            var clampedOffset = ClampPanelOffset(kind, new Point(transform.X, transform.Y));
            transform.X = clampedOffset.X;
            transform.Y = clampedOffset.Y;
            if (save)
            {
                SaveCurrentPanelGeometry(kind);
            }
        }
    }

    private void UpdateEditorAvoidanceForPanels(bool animate)
    {
        if (StudyEditorScrollViewer is null)
        {
            return;
        }

        var targetMargin = new Thickness(4, 0, 24, 0);
        var targetCaretMargin = new Thickness(4, 0, 24, 0);
        if (!animate)
        {
            StudyEditorScrollViewer.Margin = targetMargin;
            StudyEditorCaretLayer.Margin = targetCaretMargin;
            return;
        }

        StudyEditorScrollViewer.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation
        {
            To = targetMargin,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
        StudyEditorCaretLayer.BeginAnimation(FrameworkElement.MarginProperty, new ThicknessAnimation
        {
            To = targetCaretMargin,
            Duration = new Duration(TimeSpan.FromMilliseconds(180)),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
        });
    }

    private void StudyPanel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        PositionPanelPresetTargets();
        var saveGeometry = !_isInitializing && !_isRestoringStudyPanelGeometry;
        ClampAllPanelsToStudyArea(save: saveGeometry);
        ClampExtraPanelsToStudyArea(save: saveGeometry);
        if (_draggingPanelKind is null)
        {
            UpdateEditorAvoidanceForPanels(animate: false);
        }
    }

    private void ClampExtraPanelsToStudyArea(bool save)
    {
        foreach (var runtime in _extraPanelRuntimes.Values)
        {
            var geometry = ClampExtraPanelGeometry(runtime.State.Geometry);
            runtime.State.Geometry = geometry;
            runtime.Root.Width = geometry.Width;
            runtime.Root.Height = geometry.Height;
            runtime.Transform.X = geometry.X;
            runtime.Transform.Y = geometry.Y;
        }

        if (save && _extraPanelRuntimes.Count > 0)
        {
            QueueWorkspaceSave();
        }
    }

    private void HideStrongsPanel(bool animate)
    {
        if (StrongsColumn.ActualWidth <= 0 && StrongsPanelRoot.Visibility != Visibility.Visible)
        {
            return;
        }

        StopStrongsSmoothScroll();
        if (StrongsColumn.ActualWidth > 0)
        {
            _strongsPanelVisibleWidth = new GridLength(StrongsColumn.ActualWidth);
            SaveCurrentPanelGeometry(MovablePanelKind.Strongs);
        }

        if (!animate)
        {
            StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            StrongsColumn.Width = new GridLength(0);
            StrongsSplitterColumn.Width = new GridLength(0);
            StrongsColumnSplitter.Visibility = Visibility.Collapsed;
            StrongsPanelRoot.BeginAnimation(OpacityProperty, null);
            StrongsPanelRoot.Opacity = 0;
            StrongsPanelRoot.Visibility = Visibility.Collapsed;
            UpdateEditorAvoidanceForPanels(animate: false);
            return;
        }

        AnimateStrongsPanelWidth(StrongsColumn.ActualWidth, 0, show: false);
    }

    private void AnimateStrongsPanelWidth(double fromWidth, double toWidth, bool show)
    {
        var duration = TimeSpan.FromMilliseconds(show ? 190 : 150);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        if (show)
        {
            StrongsSplitterColumn.Width = new GridLength(0);
            StrongsColumnSplitter.Visibility = Visibility.Collapsed;
            StrongsColumn.MinWidth = 0;
        }

        StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        StrongsColumn.Width = new GridLength(Math.Max(0, fromWidth));
        var widthAnimation = new GridLengthAnimation
        {
            From = new GridLength(Math.Max(0, fromWidth)),
            To = new GridLength(Math.Max(0, toWidth)),
            Duration = new Duration(duration),
            EasingFunction = ease
        };
        widthAnimation.Completed += (_, _) =>
        {
            StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            StrongsColumn.Width = new GridLength(Math.Max(0, toWidth));
            if (!show)
            {
                StrongsColumn.MinWidth = 0;
                StrongsSplitterColumn.Width = new GridLength(0);
                StrongsColumnSplitter.Visibility = Visibility.Collapsed;
                StrongsPanelRoot.Visibility = Visibility.Collapsed;
                StrongsPanelRoot.Opacity = 0;
                UpdateEditorAvoidanceForPanels(animate: true);
            }
        };

        StrongsColumn.BeginAnimation(ColumnDefinition.WidthProperty, widthAnimation);
        StrongsPanelRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 0 : 1, show ? 1 : 0, duration)
        {
            EasingFunction = ease
        });
    }

    private void StrongsColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (StrongsColumn.ActualWidth > 0)
        {
            _strongsPanelVisibleWidth = new GridLength(StrongsColumn.ActualWidth);
        }
    }

    private void AnimateScripturePanelCollapseThenClose(double fromWidth)
    {
        var duration = TimeSpan.FromMilliseconds(180);
        var ease = new BackEase
        {
            EasingMode = EasingMode.EaseIn,
            Amplitude = 0.35
        };

        var scaleX = new DoubleAnimation(1, 0.04, duration) { EasingFunction = ease };
        var scaleY = new DoubleAnimation(1, 0.04, duration) { EasingFunction = ease };
        var rotate = new DoubleAnimation(0, -5, duration) { EasingFunction = ease };
        var fade = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(150))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn }
        };

        scaleX.Completed += (_, _) =>
        {
            ScripturePanelRoot.Visibility = Visibility.Collapsed;
            ScripturePanelRoot.Opacity = 1;
            ScripturePanelScale.ScaleX = 0.04;
            ScripturePanelScale.ScaleY = 0.04;
            ScripturePanelRotate.Angle = -5;
            AnimateScripturePanelWidth(fromWidth, 0, hiding: true);
        };

        ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleX);
        ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleY);
        ScripturePanelRotate.BeginAnimation(RotateTransform.AngleProperty, rotate);
        ScripturePanelRoot.BeginAnimation(OpacityProperty, fade);
    }

    private void AnimateScripturePanelWidth(double fromWidth, double toWidth, bool hiding)
    {
        var duration = TimeSpan.FromMilliseconds(hiding ? 150 : 210);
        var ease = new CubicEase { EasingMode = EasingMode.EaseInOut };

        ScriptureColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
        ScriptureColumn.Width = new GridLength(Math.Max(0, fromWidth));
        var widthAnimation = new GridLengthAnimation
        {
            From = new GridLength(Math.Max(0, fromWidth)),
            To = new GridLength(Math.Max(0, toWidth)),
            Duration = new Duration(duration),
            EasingFunction = ease
        };
        widthAnimation.Completed += (_, _) =>
        {
            ScriptureColumn.BeginAnimation(ColumnDefinition.WidthProperty, null);
            ScriptureColumn.Width = new GridLength(Math.Max(0, toWidth));
            ScriptureColumnSplitter.BeginAnimation(FrameworkElement.WidthProperty, null);
            if (hiding)
            {
                ScriptureColumnSplitter.Width = 0;
                ScriptureColumnSplitter.Visibility = Visibility.Collapsed;
                ScripturePanelRoot.Visibility = Visibility.Collapsed;
                ScripturePanelRoot.Opacity = 1;
                ScripturePanelShowButton.Visibility = Visibility.Visible;
                UpdateEditorAvoidanceForPanels(animate: true);
            }
            else
            {
                ScriptureColumn.MinWidth = 0;
                ScriptureColumnSplitter.Width = 0;
                ScriptureColumnSplitter.Visibility = Visibility.Collapsed;
                ScripturePanelRoot.Visibility = Visibility.Visible;
                ScripturePanelRoot.Opacity = 0;
                ScripturePanelScale.ScaleX = 0.04;
                ScripturePanelScale.ScaleY = 0.04;
                ScripturePanelRotate.Angle = -5;
                var revealEase = new BackEase
                {
                    EasingMode = EasingMode.EaseOut,
                    Amplitude = 0.28
                };
                ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(0.04, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = revealEase
                });
                ScripturePanelScale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(0.04, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = revealEase
                });
                ScripturePanelRotate.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(-5, 0, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                ScripturePanelRoot.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140))
                {
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                });
                Dispatcher.BeginInvoke(() => UpdateEditorAvoidanceForPanels(animate: false), DispatcherPriority.Render);
            }
        };

        ScriptureColumn.BeginAnimation(ColumnDefinition.WidthProperty, widthAnimation);
        ScriptureColumnSplitter.BeginAnimation(FrameworkElement.WidthProperty, new DoubleAnimation(
            0,
            0,
            duration)
        {
            EasingFunction = ease
        });
    }

    private void ScriptureColumnSplitter_DragCompleted(object sender, DragCompletedEventArgs e)
    {
        if (!_scripturePanelHidden && ScriptureColumn.ActualWidth > 0)
        {
            _scripturePanelVisibleWidth = new GridLength(ScriptureColumn.ActualWidth);
            SaveCurrentPanelGeometry(MovablePanelKind.Scripture);
        }
    }

    private Brush PickAccentBrush(int offset)
    {
        var selectedBookOffset = _selectedBook is null
            ? 0
            : BibleBooks.IndexOf(_selectedBook);
        return _accentPalette[(selectedBookOffset + offset + 1) % _accentPalette.Length];
    }

    private static string CreateUniqueName(string baseName, IEnumerable<WorkspaceItem> siblings)
    {
        var siblingNames = siblings.Select(item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!siblingNames.Contains(baseName))
        {
            return baseName;
        }

        var index = 2;
        while (siblingNames.Contains($"{baseName} {index}"))
        {
            index++;
        }

        return $"{baseName} {index}";
    }

    private static Brush BrushFrom(string hex)
    {
        return (Brush)new BrushConverter().ConvertFromString(hex)!;
    }

    private static T? FindParent<T>(DependencyObject source)
        where T : DependencyObject
    {
        var cursor = source;
        while (cursor is not null)
        {
            if (cursor is T match)
            {
                return match;
            }

            cursor = VisualTreeHelper.GetParent(cursor);
        }

        return null;
    }

    private static T? FindChild<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(source); index++)
        {
            var child = VisualTreeHelper.GetChild(source, index);
            if (child is T match)
            {
                return match;
            }

            var nestedMatch = FindChild<T>(child);
            if (nestedMatch is not null)
            {
                return nestedMatch;
            }
        }

        return null;
    }

    private TextBox? FindWorkspaceRenameTextBox(WorkspaceItem item)
    {
        var container = WorkspaceItems.ItemContainerGenerator.ContainerFromItem(item) as DependencyObject;
        return container is null
            ? null
            : FindChildren<TextBox>(container).FirstOrDefault(textBox => ReferenceEquals(textBox.Tag, item));
    }

    private static IEnumerable<T> FindChildren<T>(DependencyObject source)
        where T : DependencyObject
    {
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(source); index++)
        {
            var child = VisualTreeHelper.GetChild(source, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (var nestedMatch in FindChildren<T>(child))
            {
                yield return nestedMatch;
            }
        }
    }
}

public sealed record BibleBook(string Name, string Testament, int[] VerseCounts, Brush AccentBrush)
{
    public int Chapters => VerseCounts.Length;

    public string ChapterLabel => Chapters == 1 ? "1 chapter" : $"{Chapters} chapters";
}

public sealed class WorkspaceItem : INotifyPropertyChanged
{
    private string _name;
    private int? _passageStartChapter;
    private int? _passageStartVerse;
    private int? _passageEndChapter;
    private int? _passageEndVerse;
    private DateTime? _lastEditedAt;
    private bool _isRenaming;

    public WorkspaceItem(string name, WorkspaceItemKind kind, Brush accentBrush, WorkspaceItem? parent)
    {
        _name = name;
        Kind = kind;
        AccentBrush = accentBrush;
        Parent = parent;
    }

    public string Name
    {
        get => _name;
        set
        {
            if (_name == value)
            {
                return;
            }

            _name = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        private set
        {
            if (_isRenaming == value)
            {
                return;
            }

            _isRenaming = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRenaming)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayNameVisibility)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RenameTextBoxVisibility)));
        }
    }

    public string RenameOriginalName { get; private set; } = string.Empty;

    public Visibility DisplayNameVisibility => IsRenaming
        ? Visibility.Collapsed
        : Visibility.Visible;

    public Visibility RenameTextBoxVisibility => IsRenaming
        ? Visibility.Visible
        : Visibility.Collapsed;

    public WorkspaceItemKind Kind { get; }

    public Brush AccentBrush { get; }

    public WorkspaceItem? Parent { get; }

    public ObservableCollection<WorkspaceItem> Children { get; } = new();

    public ObservableCollection<StudyBlock> Blocks { get; } = new();

    public PanelGeometryState? EditorPanelGeometry { get; set; }

    public PanelGeometryState? ScripturePanelGeometry { get; set; }

    public PanelGeometryState? StrongsPanelGeometry { get; set; }

    public ObservableCollection<ExtraStudyPanelState> ExtraPanels { get; } = new();

    public ObservableCollection<TextAnnotationState> TextAnnotations { get; } = new();

    public int? PassageStartChapter
    {
        get => _passageStartChapter;
        set
        {
            if (_passageStartChapter == value)
            {
                return;
            }

            _passageStartChapter = value;
            NotifyPassageChanged();
        }
    }

    public int? PassageStartVerse
    {
        get => _passageStartVerse;
        set
        {
            if (_passageStartVerse == value)
            {
                return;
            }

            _passageStartVerse = value;
            NotifyPassageChanged();
        }
    }

    public int? PassageEndChapter
    {
        get => _passageEndChapter;
        set
        {
            if (_passageEndChapter == value)
            {
                return;
            }

            _passageEndChapter = value;
            NotifyPassageChanged();
        }
    }

    public int? PassageEndVerse
    {
        get => _passageEndVerse;
        set
        {
            if (_passageEndVerse == value)
            {
                return;
            }

            _passageEndVerse = value;
            NotifyPassageChanged();
        }
    }

    public DateTime? LastEditedAt
    {
        get => _lastEditedAt;
        set
        {
            if (_lastEditedAt == value)
            {
                return;
            }

            _lastEditedAt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LastEditedAt)));
        }
    }

    public string KindLabel => Kind switch
    {
        WorkspaceItemKind.Book => "Book",
        WorkspaceItemKind.Folder => "Folder",
        _ => "Study"
    };

    public string DetailLabel => Kind switch
    {
        WorkspaceItemKind.Folder => Children.Count == 1 ? "1 item" : $"{Children.Count} items",
        WorkspaceItemKind.Study => PassageLabel,
        _ => string.Empty
    };

    public string PassageLabel
    {
        get
        {
            if (PassageStartChapter is null || PassageStartVerse is null)
            {
                return "No passage selected";
            }

            if (PassageEndChapter is null || PassageEndVerse is null)
            {
                return $"{PassageStartChapter}:{PassageStartVerse}";
            }

            if (PassageStartChapter == PassageEndChapter && PassageStartVerse == PassageEndVerse)
            {
                return $"{PassageStartChapter}:{PassageStartVerse}";
            }

            return $"{PassageStartChapter}:{PassageStartVerse}-{PassageEndChapter}:{PassageEndVerse}";
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshDetail()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailLabel)));
    }

    public void BeginRename()
    {
        RenameOriginalName = Name;
        IsRenaming = true;
    }

    public void EndRename()
    {
        RenameOriginalName = string.Empty;
        IsRenaming = false;
    }

    public void CancelRename()
    {
        if (!string.IsNullOrWhiteSpace(RenameOriginalName))
        {
            Name = RenameOriginalName;
        }

        EndRename();
    }

    private void NotifyPassageChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageStartChapter)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageStartVerse)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageEndChapter)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageEndVerse)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PassageLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DetailLabel)));
    }
}

public enum WorkspaceItemKind
{
    Book,
    Folder,
    Study
}

public enum BreadcrumbTarget
{
    Library,
    Today,
    ReminderSettings,
    Settings,
    ColorThemeSettings,
    LocalAiSettings,
    WorkspaceItem,
    Study
}

public enum AppNavTab
{
    BibleStudy,
    Today,
    ScriptureMemory,
    ReminderSettings,
    Settings
}

public enum EditorBlockKind
{
    Normal,
    HeadingOne,
    HeadingTwo,
    HeadingThree,
    Quote,
    BulletedList,
    NumberedList,
    Todo,
    Callout,
    Code
}

public sealed class StudyBlock : INotifyPropertyChanged
{
    private Brush _normalBrush;
    private Brush _secondaryBrush;
    private Brush _accentBrush;
    private Brush _panelBrush;
    private Brush _appBrush;
    private Brush _coralBrush;
    private string _text = string.Empty;
    private EditorBlockKind _kind;
    private bool _isFocused;
    private bool _isChecked;
    private string _numberLabel = "1.";

    public StudyBlock(
        EditorBlockKind kind,
        Brush normalBrush,
        Brush secondaryBrush,
        Brush accentBrush,
        Brush panelBrush,
        Brush appBrush,
        Brush coralBrush)
    {
        _kind = kind;
        _normalBrush = normalBrush;
        _secondaryBrush = secondaryBrush;
        _accentBrush = accentBrush;
        _panelBrush = panelBrush;
        _appBrush = appBrush;
        _coralBrush = coralBrush;
    }

    public void UpdateThemeBrushes(
        Brush normalBrush,
        Brush secondaryBrush,
        Brush accentBrush,
        Brush panelBrush,
        Brush appBrush,
        Brush coralBrush)
    {
        _normalBrush = normalBrush;
        _secondaryBrush = secondaryBrush;
        _accentBrush = accentBrush;
        _panelBrush = panelBrush;
        _appBrush = appBrush;
        _coralBrush = coralBrush;
        NotifyStyleChanged();
    }

    public string Text
    {
        get => _text;
        set
        {
            if (_text == value)
            {
                return;
            }

            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public bool IsFocused
    {
        get => _isFocused;
        set
        {
            if (_isFocused == value)
            {
                return;
            }

            _isFocused = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderBrush)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderThickness)));
        }
    }

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value)
            {
                return;
            }

            _isChecked = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsChecked)));
        }
    }

    public string NumberLabel
    {
        get => _numberLabel;
        set
        {
            if (_numberLabel == value)
            {
                return;
            }

            _numberLabel = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NumberLabel)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerText)));
        }
    }

    public EditorBlockKind Kind
    {
        get => _kind;
        set
        {
            if (_kind == value)
            {
                return;
            }

            _kind = value;
            NotifyStyleChanged();
        }
    }

    public double FontSize => Kind switch
    {
        EditorBlockKind.HeadingOne => 34,
        EditorBlockKind.HeadingTwo => 25,
        EditorBlockKind.HeadingThree => 20,
        EditorBlockKind.Quote => 17,
        EditorBlockKind.Code => 15,
        _ => 16
    };

    public FontWeight FontWeight => Kind switch
    {
        EditorBlockKind.HeadingOne => FontWeights.Black,
        EditorBlockKind.HeadingTwo => FontWeights.Bold,
        EditorBlockKind.HeadingThree => FontWeights.Bold,
        _ => FontWeights.Normal
    };

    public FontStyle FontStyle => Kind == EditorBlockKind.Quote
        ? FontStyles.Italic
        : FontStyles.Normal;

    public FontFamily FontFamily => Kind == EditorBlockKind.Code
        ? new FontFamily("Consolas")
        : new FontFamily("Segoe UI");

    public Brush ForegroundBrush => _normalBrush;

    public Brush BackgroundBrush => Kind switch
    {
        EditorBlockKind.Quote => _panelBrush,
        EditorBlockKind.Callout => _panelBrush,
        EditorBlockKind.Code => _appBrush,
        _ => IsFocused
            ? _panelBrush
            : Brushes.Transparent
    };

    public Brush BorderBrush => Kind switch
    {
        EditorBlockKind.Quote => _accentBrush,
        EditorBlockKind.Callout => _accentBrush,
        EditorBlockKind.Code => _coralBrush,
        _ => Brushes.Transparent
    };

    public Thickness BorderThickness => Kind switch
    {
        EditorBlockKind.Quote => new Thickness(4, 0, 0, 0),
        EditorBlockKind.Callout => new Thickness(1),
        EditorBlockKind.Code => new Thickness(1),
        _ => new Thickness(0)
    };

    public Thickness Padding => Kind switch
    {
        EditorBlockKind.Quote => new Thickness(14, 8, 0, 8),
        EditorBlockKind.Callout => new Thickness(14, 10, 14, 10),
        EditorBlockKind.Code => new Thickness(14, 10, 14, 10),
        _ => new Thickness(10, 6, 10, 6)
    };

    public Thickness Margin => Kind switch
    {
        EditorBlockKind.HeadingOne => new Thickness(0, 10, 0, 16),
        EditorBlockKind.HeadingTwo => new Thickness(0, 8, 0, 14),
        EditorBlockKind.HeadingThree => new Thickness(0, 6, 0, 12),
        EditorBlockKind.Quote => new Thickness(0, 8, 0, 16),
        EditorBlockKind.Callout => new Thickness(0, 8, 0, 14),
        EditorBlockKind.Code => new Thickness(0, 8, 0, 14),
        _ => new Thickness(0, 0, 0, 14)
    };

    public double MinHeight => Kind switch
    {
        EditorBlockKind.HeadingOne => 46,
        EditorBlockKind.HeadingTwo => 36,
        EditorBlockKind.HeadingThree => 32,
        EditorBlockKind.Quote => 44,
        EditorBlockKind.Callout => 44,
        EditorBlockKind.Code => 44,
        _ => 28
    };

    public double TextMinHeight => Math.Max(22, MinHeight - Padding.Top - Padding.Bottom);

    public GridLength MarkerWidth => Kind switch
    {
        EditorBlockKind.Todo => new GridLength(30),
        EditorBlockKind.Callout => new GridLength(30),
        _ => new GridLength(0)
    };

    public string MarkerText => Kind switch
    {
        EditorBlockKind.Callout => "!",
        _ => string.Empty
    };

    public Visibility MarkerVisibility => Kind == EditorBlockKind.Callout
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Visibility CheckboxVisibility => Kind == EditorBlockKind.Todo
        ? Visibility.Visible
        : Visibility.Collapsed;

    public Brush MarkerBrush => _normalBrush;

    public double MarkerFontSize => Kind == EditorBlockKind.BulletedList
        ? 20
        : 16;

    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyStyleChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontSize)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontWeight)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontStyle)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FontFamily)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ForegroundBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BackgroundBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(BorderThickness)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Padding)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Margin)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MinHeight)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TextMinHeight)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerWidth)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerText)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CheckboxVisibility)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerBrush)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MarkerFontSize)));
    }
}

public sealed record EditorCommandOption(
    string Title,
    string Description,
    string Shortcut,
    string SearchTerms,
    EditorBlockKind Kind,
    Brush AccentBrush)
{
    public bool Matches(string query)
    {
        return string.IsNullOrWhiteSpace(query)
            || SearchTerms.Contains(query, StringComparison.OrdinalIgnoreCase)
            || Title.Contains(query, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed record BreadcrumbPayload(BreadcrumbTarget Target, object? Value);

public sealed class GridLengthAnimation : AnimationTimeline
{
    public static readonly DependencyProperty FromProperty = DependencyProperty.Register(
        nameof(From),
        typeof(GridLength),
        typeof(GridLengthAnimation));

    public static readonly DependencyProperty ToProperty = DependencyProperty.Register(
        nameof(To),
        typeof(GridLength),
        typeof(GridLengthAnimation));

    public GridLength From
    {
        get => (GridLength)GetValue(FromProperty);
        set => SetValue(FromProperty, value);
    }

    public GridLength To
    {
        get => (GridLength)GetValue(ToProperty);
        set => SetValue(ToProperty, value);
    }

    public IEasingFunction? EasingFunction { get; set; }

    public override Type TargetPropertyType => typeof(GridLength);

    protected override Freezable CreateInstanceCore()
    {
        return new GridLengthAnimation();
    }

    public override object GetCurrentValue(object defaultOriginValue, object defaultDestinationValue, AnimationClock animationClock)
    {
        var progress = animationClock.CurrentProgress ?? 0;
        if (EasingFunction is not null)
        {
            progress = EasingFunction.Ease(progress);
        }

        var from = From.Value;
        var to = To.Value;
        return new GridLength(from + ((to - from) * progress), GridUnitType.Pixel);
    }
}

public sealed class WorkspaceState
{
    public List<WorkspaceItemState> Books { get; set; } = new();

    public List<DailyNoteState> DailyNotes { get; set; } = new();

    public List<ScheduledNotificationState> ScheduledNotifications { get; set; } = new();

    public List<ScriptureMemoryState> ScriptureMemoryPassages { get; set; } = new();

    public bool OldTestamentBooksExpanded { get; set; } = true;

    public bool NewTestamentBooksExpanded { get; set; } = true;

    public ReminderSettingsState ReminderSettings { get; set; } = new();

    public ColorSettingsState ColorSettings { get; set; } = new();

    public WindowPlacementState? WindowPlacement { get; set; }
}

public sealed class ScriptureMemoryState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string BookName { get; set; } = string.Empty;

    public int Chapter { get; set; }

    public int StartChapter { get; set; }

    public int StartVerse { get; set; }

    public int EndChapter { get; set; }

    public int EndVerse { get; set; }

    public string Translation { get; set; } = "ASV";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public DateTimeOffset? LastPracticedAt { get; set; }

    public List<ScriptureMemorySectionState> Sections { get; set; } = new();

    [JsonIgnore]
    public string Reference => FormatReference(BookName, StartChapter, StartVerse, EndChapter, EndVerse);

    [JsonIgnore]
    public string Preview
    {
        get
        {
            var text = string.Join(" ", Sections.Select(section => section.Text)).Trim();
            return text.Length > 180 ? $"{text[..177]}…" : text;
        }
    }

    [JsonIgnore]
    public string StageLabel => GetStageLabel(Sections.Count == 0 ? 0 : Sections.Min(section => section.CueLevel));

    [JsonIgnore]
    public string PracticeDetail
    {
        get
        {
            var attempts = Sections.Sum(section => section.Attempts);
            if (LastPracticedAt is null)
            {
                return $"{Sections.Count} {(Sections.Count == 1 ? "verse" : "verses")} · not practiced yet";
            }
            return $"{Sections.Count} {(Sections.Count == 1 ? "verse" : "verses")} · {attempts} {(attempts == 1 ? "attempt" : "attempts")}";
        }
    }

    public static string GetStageLabel(int cueLevel) => cueLevel switch
    {
        1 => "Familiar · 70% cues",
        2 => "Recall · 40% cues",
        3 => "Minimal cues · 15% cues",
        4 => "Memorized · no cues",
        _ => "Learn · all words visible"
    };

    public static string FormatReference(string bookName, int startChapter, int startVerse, int endChapter, int endVerse)
    {
        if (startChapter == endChapter && startVerse == endVerse)
        {
            return $"{bookName} {startChapter}:{startVerse}";
        }
        return startChapter == endChapter
            ? $"{bookName} {startChapter}:{startVerse}–{endVerse}"
            : $"{bookName} {startChapter}:{startVerse} through {endChapter}:{endVerse}";
    }

    public void NormalizeLegacyRange()
    {
        StartChapter = StartChapter > 0 ? StartChapter : Chapter;
        EndChapter = EndChapter > 0 ? EndChapter : StartChapter;
        Chapter = StartChapter;
        foreach (var section in Sections)
        {
            section.Chapter = section.Chapter > 0 ? section.Chapter : StartChapter;
        }
    }
}

public sealed class ScriptureMemorySectionState
{
    public int Chapter { get; set; }

    public int Verse { get; set; }

    public string Text { get; set; } = string.Empty;

    public int CueLevel { get; set; }

    public int SuccessfulRepetitions { get; set; }

    public int Attempts { get; set; }
}

public sealed class WindowPlacementState
{
    public double Left { get; set; }

    public double Top { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }

    public bool IsMaximized { get; set; }
}

public sealed class ColorSettingsState
{
    public string SelectedSchemeName { get; set; } = "Olive Study";

    public List<ColorSchemePreset> Presets { get; set; } = new();
}

public sealed record ColorSchemePreset(
    string Name,
    string AppBackground,
    string SidebarBackground,
    string PanelBackground,
    string Accent,
    string TextPrimary,
    string TextSecondary,
    bool BuiltIn)
{
    [JsonIgnore]
    public Brush AppBackgroundBrush => BrushFromHex(AppBackground);

    [JsonIgnore]
    public Brush SidebarBackgroundBrush => BrushFromHex(SidebarBackground);

    [JsonIgnore]
    public Brush PanelBackgroundBrush => BrushFromHex(PanelBackground);

    [JsonIgnore]
    public Brush AccentBrush => BrushFromHex(Accent);

    [JsonIgnore]
    public Brush TextPrimaryBrush => BrushFromHex(TextPrimary);

    [JsonIgnore]
    public Brush TextSecondaryBrush => BrushFromHex(TextSecondary);

    private static Brush BrushFromHex(string hex)
    {
        try
        {
            return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex.Trim()));
        }
        catch
        {
            return Brushes.Transparent;
        }
    }
}

public sealed class WorkspaceItemState
{
    public string Name { get; set; } = string.Empty;

    public WorkspaceItemKind Kind { get; set; }

    public string AccentHex { get; set; } = "#FFFFFFFF";

    public int? PassageStartChapter { get; set; }

    public int? PassageStartVerse { get; set; }

    public int? PassageEndChapter { get; set; }

    public int? PassageEndVerse { get; set; }

    public DateTime? LastEditedAt { get; set; }

    public PanelGeometryState? EditorPanelGeometry { get; set; }

    public PanelGeometryState? ScripturePanelGeometry { get; set; }

    public PanelGeometryState? StrongsPanelGeometry { get; set; }

    public List<ExtraStudyPanelState> ExtraPanels { get; set; } = new();

    public List<TextAnnotationState> TextAnnotations { get; set; } = new();

    public List<WorkspaceItemState> Children { get; set; } = new();

    public List<StudyBlockState> Blocks { get; set; } = new();
}

public sealed class StudyBlockState
{
    public EditorBlockKind Kind { get; set; }

    public string Text { get; set; } = string.Empty;

    public bool IsChecked { get; set; }
}

public sealed class TextAnnotationState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string SourceKey { get; set; } = string.Empty;

    public int Start { get; set; }

    public int Length { get; set; }

    public string Quote { get; set; } = string.Empty;

    public bool IsHighlighted { get; set; }

    public string HighlightColor { get; set; } = "#F4C95D";

    public string Note { get; set; } = string.Empty;
}

public sealed class PanelGeometryState
{
    public double X { get; set; }

    public double Y { get; set; }

    public double Width { get; set; }

    public double Height { get; set; }
}

public enum ExtraStudyPanelKind
{
    Scripture,
    Notes
}

public sealed class ExtraStudyPanelState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public ExtraStudyPanelKind Kind { get; set; }

    public string Title { get; set; } = string.Empty;

    public PanelGeometryState Geometry { get; set; } = new();

    public string? BookName { get; set; }

    public int? Chapter { get; set; }

    public int? SelectedVerse { get; set; }

    public string NotesText { get; set; } = string.Empty;
}

public sealed class TextAnnotationAdorner : Adorner
{
    private readonly TextBox _textBox;
    private readonly Func<IReadOnlyList<TextAnnotationState>> _annotations;

    public TextAnnotationAdorner(TextBox textBox, Func<IReadOnlyList<TextAnnotationState>> annotations)
        : base(textBox)
    {
        _textBox = textBox;
        _annotations = annotations;
        IsHitTestVisible = false;
        ClipToBounds = true;
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        if (string.IsNullOrEmpty(_textBox.Text))
        {
            return;
        }

        var underlineBrush = new SolidColorBrush(Color.FromRgb(188, 108, 37));
        underlineBrush.Freeze();
        var underlinePen = new Pen(underlineBrush, 2.1);
        underlinePen.Freeze();

        foreach (var annotation in _annotations())
        {
            var start = Math.Clamp(annotation.Start, 0, _textBox.Text.Length);
            var end = Math.Clamp(annotation.Start + annotation.Length, start, _textBox.Text.Length);
            if (end <= start)
            {
                continue;
            }

            Rect? lineRect = null;
            for (var index = start; index < end; index++)
            {
                var leading = _textBox.GetRectFromCharacterIndex(index, true);
                var trailing = _textBox.GetRectFromCharacterIndex(index, false);
                if (leading.IsEmpty || trailing.IsEmpty)
                {
                    continue;
                }

                var charRect = new Rect(leading.X, leading.Y, Math.Max(2, trailing.X - leading.X), Math.Max(leading.Height, trailing.Height));
                if (lineRect is { } current && Math.Abs(current.Y - charRect.Y) < 1.5)
                {
                    lineRect = Rect.Union(current, charRect);
                }
                else
                {
                    DrawAnnotationLine(drawingContext, lineRect, annotation, CreateHighlightBrush(annotation), underlinePen);
                    lineRect = charRect;
                }
            }
            DrawAnnotationLine(drawingContext, lineRect, annotation, CreateHighlightBrush(annotation), underlinePen);
        }
    }

    private static Brush CreateHighlightBrush(TextAnnotationState annotation)
    {
        try
        {
            var color = (Color)ColorConverter.ConvertFromString(annotation.HighlightColor);
            var brush = new SolidColorBrush(Color.FromArgb(76, color.R, color.G, color.B));
            brush.Freeze();
            return brush;
        }
        catch
        {
            return new SolidColorBrush(Color.FromArgb(76, 244, 201, 93));
        }
    }

    private static void DrawAnnotationLine(DrawingContext drawingContext, Rect? lineRect, TextAnnotationState annotation,
        Brush highlightBrush, Pen underlinePen)
    {
        if (lineRect is not { } rect || rect.IsEmpty)
        {
            return;
        }
        if (annotation.IsHighlighted)
        {
            drawingContext.DrawRoundedRectangle(highlightBrush, null,
                new Rect(rect.X, rect.Y + 1, rect.Width, Math.Max(2, rect.Height - 2)), 3, 3);
        }
        if (!string.IsNullOrWhiteSpace(annotation.Note))
        {
            var y = rect.Bottom - 1;
            drawingContext.DrawLine(underlinePen, new Point(rect.Left, y), new Point(rect.Right, y));
        }
    }
}

public sealed record ExtraPanelRuntime(
    ExtraStudyPanelState State,
    Border Root,
    TranslateTransform Transform);

public sealed class DailyNoteState
{
    public string DateKey { get; set; } = string.Empty;

    public string Details { get; set; } = string.Empty;

    public List<string> ExcludedStudyContextKeys { get; set; } = new();
}

public sealed class ScheduledNotificationState
{
    public string SequenceId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Delivery { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public bool Deleted { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}

public sealed class ReminderSettingsState
{
    public string AiModel { get; set; } = "llama3.2:latest";

    public bool OverviewEnabled { get; set; } = true;

    public string OverviewPrompt { get; set; } = string.Empty;

    public bool RemindersEnabled { get; set; } = true;

    public List<ReminderScheduleItemState> Reminders { get; set; } = new();

    public List<ReminderPresetState> Presets { get; set; } = new();
}

public sealed class ReminderPresetState
{
    public string Name { get; set; } = string.Empty;

    public List<ReminderScheduleItemState> Reminders { get; set; } = new();
}

public sealed class ReminderScheduleItemState
{
    public int DelayAmount { get; set; } = 1;

    public string DelayUnit { get; set; } = "hours";

    public string Prompt { get; set; } = string.Empty;
}

public sealed class ReminderPreset
{
    public ReminderPreset(string name, List<ReminderScheduleItem> reminders)
    {
        Name = name;
        Reminders = reminders;
    }

    public string Name { get; }

    public List<ReminderScheduleItem> Reminders { get; }
}

public sealed class ReminderScheduleItem : INotifyPropertyChanged
{
    private int _delayAmount;
    private string _delayUnit;
    private string _prompt;

    public ReminderScheduleItem(int delayAmount, string delayUnit, string prompt)
    {
        _delayAmount = Math.Max(1, delayAmount);
        _delayUnit = delayUnit;
        _prompt = prompt;
    }

    public int DelayAmount
    {
        get => _delayAmount;
        set
        {
            var nextValue = Math.Max(1, value);
            if (_delayAmount == nextValue)
            {
                return;
            }

            _delayAmount = nextValue;
            NotifyChanged();
        }
    }

    public string DelayUnit
    {
        get => _delayUnit;
        set
        {
            var nextValue = value;
            if (_delayUnit == nextValue)
            {
                return;
            }

            _delayUnit = nextValue;
            NotifyChanged();
        }
    }

    public string Prompt
    {
        get => _prompt;
        set
        {
            if (_prompt == value)
            {
                return;
            }

            _prompt = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Prompt)));
        }
    }

    public string DelayLabel => $"After {DelayAmount} {DelayUnit}";

    public string NtfyDelay => DelayUnit switch
    {
        "minutes" => DelayAmount == 1 ? "1 minute" : $"{DelayAmount} minutes",
        "days" => DelayAmount == 1 ? "1 day" : $"{DelayAmount} days",
        _ => DelayAmount == 1 ? "1 hour" : $"{DelayAmount} hours"
    };

    public event PropertyChangedEventHandler? PropertyChanged;

    public ReminderScheduleItem Clone()
    {
        return new ReminderScheduleItem(DelayAmount, DelayUnit, Prompt);
    }

    private void NotifyChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayAmount)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayUnit)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DelayLabel)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(NtfyDelay)));
    }
}

public sealed record DailyStudyEntry(
    WorkspaceItem Study,
    string StudyName,
    string LocationLabel,
    string PassageLabel,
    string LastEditedLabel,
    string ContextKey);

public sealed class ScheduledNotificationDisplay
{
    public ScheduledNotificationDisplay(ScheduledNotificationState state)
    {
        SequenceId = state.SequenceId;
        Title = string.IsNullOrWhiteSpace(state.Title)
            ? "Scheduled message"
            : state.Title;
        Detail = $"{state.Delivery} - {state.SequenceId}";
        StatusLabel = state.Deleted ? "Deleted" : "Active";
        StatusBrush = state.Deleted
            ? new SolidColorBrush(Color.FromRgb(214, 201, 157))
            : new SolidColorBrush(Color.FromRgb(221, 161, 94));
        RemoveVisibility = state.Deleted
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    public string SequenceId { get; }

    public string Title { get; }

    public string Detail { get; }

    public string StatusLabel { get; }

    public Brush StatusBrush { get; }

    public Visibility RemoveVisibility { get; }
}

public sealed class ScriptureVerseDisplay
{
    public ScriptureVerseDisplay(int verseNumber, string text, bool isSelected)
    {
        VerseNumber = verseNumber;
        Text = text;
        IsSelected = isSelected;
    }

    public int VerseNumber { get; }

    public string Text { get; }

    public string DisplayText => $"{VerseNumber} {Text}";

    public bool IsSelected { get; }

    public Brush BackgroundBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(221, 161, 94))
        : Brushes.Transparent;

    public Brush BorderBrush => IsSelected
        ? new SolidColorBrush(Color.FromRgb(17, 17, 17))
        : Brushes.Transparent;

    public Thickness BorderThickness => IsSelected
        ? new Thickness(1)
        : new Thickness(0);
}

public sealed record ScriptureDataSource(string Label, string Path);

public readonly record struct ScriptureReferenceKey(string BookName, int Chapter, int Verse);

public sealed class TagntWordEntry
{
    public TagntWordEntry(int wordIndex, string greek)
    {
        WordIndex = wordIndex;
        Greek = greek;
    }

    public int WordIndex { get; }

    public string Greek { get; set; }

    public string English { get; set; } = string.Empty;

    public string DStrong { get; set; } = string.Empty;

    public string Grammar { get; set; } = string.Empty;

    public string DictionaryForm { get; set; } = string.Empty;

    public string Gloss { get; set; } = string.Empty;

    public string WordType { get; set; } = string.Empty;

    public string Editions { get; set; } = string.Empty;

    public string VariantNotes { get; set; } = string.Empty;

    public string SubMeaning { get; set; } = string.Empty;

    public string ConjoinedWord { get; set; } = string.Empty;

    public string SimpleStrong { get; set; } = string.Empty;

    public string AltStrongs { get; set; } = string.Empty;
}

public sealed record TagntTextMatch(int Start, int Length, TagntWordEntry Entry);

public sealed record StrongsSelection(
    string BookName,
    int Chapter,
    int Verse,
    string Phrase,
    TagntWordEntry Entry);

public sealed record GreekLexiconEntry(
    string EStrong,
    string DStrong,
    string UStrong,
    string Greek,
    string Transliteration,
    string Morph,
    string Gloss,
    string Meaning);

public sealed class BibleTranslationFile
{
    public List<BibleTranslationVerse> Verses { get; set; } = new();
}

public sealed class BibleTranslationVerse
{
    [JsonPropertyName("book_name")]
    public string BookName { get; set; } = string.Empty;

    [JsonPropertyName("chapter")]
    public int Chapter { get; set; }

    [JsonPropertyName("verse")]
    public int Verse { get; set; }

    [JsonPropertyName("text")]
    public string Text { get; set; } = string.Empty;
}

public sealed record EndStudySessionApiRequest(
    string StudyName,
    string PassageReference,
    string ScriptureText,
    string NotesText,
    string AiModel,
    bool OverviewEnabled,
    string OverviewPrompt,
    bool RemindersEnabled,
    List<StudyReminderApiRequest> Reminders);

public sealed record StudyReminderApiRequest(
    string Key,
    string Title,
    string Instruction,
    string Delay);

public sealed record EndStudySessionApiResponse(
    string Overview,
    List<NotificationDispatchApiResult> Notifications);

public sealed record OllamaStatusApiResponse(
    bool Running,
    string? ExecutablePath,
    string ConfiguredModel,
    List<string> Models,
    string? Error);

public sealed record CancelNotificationsApiRequest(
    List<string> SequenceIds);

public sealed record CancelNotificationsApiResponse(
    List<NotificationCancelApiResult> Notifications);

public sealed record NotificationDispatchApiResult(
    string Title,
    string Delivery,
    string SequenceId,
    bool Sent,
    string? Error);

public sealed record NotificationCancelApiResult(
    string SequenceId,
    bool Canceled,
    string? Error);
