using Microsoft.Maui.Controls.Shapes;

namespace Gymers;

public partial class MainPage : ContentPage
{
    private readonly Color _ink = Color.FromArgb("#18212F");
    private readonly Color _muted = Color.FromArgb("#667085");
    private readonly Color _line = Color.FromArgb("#D0D5DD");
    private readonly Color _surface = Color.FromArgb("#F5F7FA");
    private readonly Color _panel = Colors.White;
    private readonly Color _accent = Color.FromArgb("#0F766E");
    private readonly Color _accentDark = Color.FromArgb("#134E4A");
    private readonly Color _warning = Color.FromArgb("#B54708");
    private VerticalStackLayout? _content;
    private Label? _sectionTitle;
    private Label? _activityStatus;
    private int _attendanceCount = 42;

    public MainPage()
    {
        InitializeComponent();
        BackgroundColor = _surface;
        Content = BuildLoginView();
    }

    private View BuildLoginView()
    {
        var root = new Grid
        {
            Padding = new Thickness(56),
            BackgroundColor = _surface,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(new GridLength(430))
            }
        };

        var intro = new VerticalStackLayout
        {
            Spacing = 18,
            VerticalOptions = LayoutOptions.Center,
            Children =
            {
                Label("GYMERS", 14, _accentDark, true),
                Label("Gym Management System", 42, _ink, true),
                Label("Mac Catalyst prototype for daily gym operations: member records, payments, attendance, trainer assignments, equipment status, and reports.", 18, _muted),
                BuildFeatureList()
            }
        };

        var username = Entry("Username", "admin");
        var password = Entry("Password", "admin123");
        password.IsPassword = true;

        var signInAdmin = PrimaryButton("Sign in as Admin");
        signInAdmin.Clicked += (_, _) => ShowApplication("Admin");

        var signInStaff = SecondaryButton("Sign in as Staff");
        signInStaff.Clicked += (_, _) => ShowApplication("Staff");

        var card = Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Project Login", 26, _ink, true),
                Label("Demo accounts are wired for presentation and screenshots.", 13, _muted),
                Field("Username", username),
                Field("Password", password),
                signInAdmin,
                signInStaff,
                Label("Admin: full access | Staff: check-in, search, and payments", 12, _muted)
            }
        });

        root.Add(intro, 0, 0);
        root.Add(card, 1, 0);
        return root;
    }

    private View BuildFeatureList()
    {
        var grid = new Grid
        {
            RowSpacing = 10,
            ColumnSpacing = 10,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            }
        };

        var items = new[]
        {
            "Role-based login",
            "Member search",
            "Payment recording",
            "Attendance check-in",
            "Trainer assignment",
            "Equipment tracking"
        };

        for (var i = 0; i < items.Length; i++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.Add(Pill(items[i], Color.FromArgb("#ECFDF3"), Color.FromArgb("#027A48")), i % 2, i / 2);
        }

        return grid;
    }

    private void ShowApplication(string role)
    {
        var root = new Grid
        {
            BackgroundColor = _surface,
            RowDefinitions =
            {
                new RowDefinition(new GridLength(72)),
                new RowDefinition(GridLength.Star)
            },
            ColumnDefinitions =
            {
                new ColumnDefinition(new GridLength(230)),
                new ColumnDefinition(GridLength.Star)
            }
        };

        var top = new Grid
        {
            Padding = new Thickness(24, 10),
            BackgroundColor = Colors.White,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Auto)
            }
        };
        top.Add(new VerticalStackLayout
        {
            Spacing = 1,
            Children =
            {
                Label("Gymers", 24, _accentDark, true),
                Label("Mac Catalyst progress build - " + DateTime.Now.ToString("MMM dd, yyyy"), 12, _muted)
            }
        }, 0, 0);
        top.Add(Pill(role + " session", Color.FromArgb("#EFF8FF"), Color.FromArgb("#175CD3")), 1, 0);

        var nav = new VerticalStackLayout
        {
            Padding = new Thickness(16, 20),
            Spacing = 8,
            BackgroundColor = Color.FromArgb("#102A43")
        };

        foreach (var section in new[] { "Dashboard", "Members", "Payments", "Attendance", "Trainers", "Workout Plans", "Equipment", "Reports" })
        {
            var button = new Button
            {
                Text = section,
                HorizontalOptions = LayoutOptions.Fill,
                BackgroundColor = section == "Dashboard" ? Color.FromArgb("#1E3A5F") : Colors.Transparent,
                TextColor = Colors.White,
                BorderColor = Color.FromArgb("#38546E"),
                BorderWidth = 1,
                CornerRadius = 6,
                Padding = new Thickness(12, 8)
            };
            button.Clicked += (_, _) => RenderSection(section);
            nav.Children.Add(button);
        }

        _sectionTitle = Label("Dashboard", 30, _ink, true);
        _content = new VerticalStackLayout { Spacing = 18 };

        var body = new ScrollView
        {
            Padding = new Thickness(24),
            Content = new VerticalStackLayout
            {
                Spacing = 20,
                Children =
                {
                    _sectionTitle,
                    _content
                }
            }
        };

        root.Add(top, 0, 0);
        Grid.SetColumnSpan(top, 2);
        root.Add(nav, 0, 1);
        root.Add(body, 1, 1);

        Content = root;
        RenderSection("Dashboard");
    }

    private void RenderSection(string section)
    {
        if (_content is null || _sectionTitle is null)
        {
            return;
        }

        _sectionTitle.Text = section;
        _content.Children.Clear();

        switch (section)
        {
            case "Dashboard":
                RenderDashboard();
                break;
            case "Members":
                RenderMembers();
                break;
            case "Payments":
                RenderPayments();
                break;
            case "Attendance":
                RenderAttendance();
                break;
            case "Trainers":
                RenderTrainers();
                break;
            case "Workout Plans":
                RenderWorkoutPlans();
                break;
            case "Equipment":
                RenderEquipment();
                break;
            default:
                RenderReports();
                break;
        }
    }

    private void RenderDashboard()
    {
        _content!.Children.Add(new Grid
        {
            ColumnSpacing = 14,
            ColumnDefinitions =
            {
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star),
                new ColumnDefinition(GridLength.Star)
            },
            Children =
            {
                Metric("Total Members", "128", "14 expiring this week", "#EFF8FF", "#175CD3", 0),
                Metric("Today's Check-ins", _attendanceCount.ToString(), "Search-by-name flow working", "#ECFDF3", "#027A48", 1),
                Metric("Monthly Income", "PHP 184,500", "Receipt flow in progress", "#FFFAEB", "#B54708", 2),
                Metric("Equipment Alerts", "3", "Maintenance follow-up", "#FEF3F2", "#B42318", 3)
            }
        });

        _content.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Label("Completed Features", 20, _ink, true),
                Bullet("Mac Catalyst project scaffolded and running as a desktop app."),
                Bullet("Role-based login screen for Admin and Staff demo access."),
                Bullet("Navigation shell for dashboard, member, payment, attendance, trainer, workout, equipment, and reports."),
                Bullet("Clickable attendance action updates the check-in count during the demo.")
            }
        }));

        _content.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Label("Current Sprint", 20, _ink, true),
                ProgressLine("SQLite persistence", 0.35),
                ProgressLine("Receipt PDF generation", 0.45),
                ProgressLine("Validation and role permissions", 0.55)
            }
        }));
    }

    private void RenderMembers()
    {
        var action = PrimaryButton("Register Sample Member");
        action.Clicked += (_, _) => _activityStatus!.Text = "Sample member saved locally for this demo session.";
        _activityStatus = Label("Ready to add or search member records.", 13, _muted);

        _content!.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Member Management", 20, _ink, true),
                Label("Search, registration, membership status, and member details are prepared for database wiring.", 13, _muted),
                new HorizontalStackLayout { Spacing = 10, Children = { Entry("Search member", "Mark"), action } },
                _activityStatus,
                Table(new[] { "Name", "Plan", "Status", "Expires" }, new[]
                {
                    new[] { "Alyssa Cruz", "Monthly", "Active", "May 28, 2026" },
                    new[] { "Mark Reyes", "Quarterly", "Active", "Jul 14, 2026" },
                    new[] { "Nina Santos", "Annual", "Expiring", "May 06, 2026" }
                })
            }
        }));
    }

    private void RenderPayments()
    {
        var save = PrimaryButton("Record Payment");
        save.Clicked += (_, _) => _activityStatus!.Text = "Payment recorded: RCPT-2026-0004 for PHP 1,500.";
        _activityStatus = Label("Waiting for payment details.", 13, _muted);

        _content!.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Payment Processing", 20, _ink, true),
                new Grid
                {
                    ColumnSpacing = 10,
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Children =
                    {
                        Entry("Member", "Alyssa Cruz").At(0),
                        Entry("Amount", "1500").At(1),
                        Entry("Method", "GCash").At(2),
                        save.At(3)
                    }
                },
                _activityStatus,
                Table(new[] { "Receipt", "Member", "Type", "Amount" }, new[]
                {
                    new[] { "RCPT-2026-0001", "Alyssa Cruz", "Renewal", "PHP 1,500" },
                    new[] { "RCPT-2026-0002", "Jomar Lim", "Walk-in", "PHP 150" },
                    new[] { "RCPT-2026-0003", "Nina Santos", "Annual", "PHP 14,000" }
                })
            }
        }));
    }

    private void RenderAttendance()
    {
        var checkIn = PrimaryButton("Check In Mark Reyes");
        checkIn.Clicked += (_, _) =>
        {
            _attendanceCount++;
            _activityStatus!.Text = "Mark Reyes checked in at " + DateTime.Now.ToString("h:mm tt") + ". Today's total: " + _attendanceCount;
        };
        _activityStatus = Label("Search a member, then record check-in.", 13, _muted);

        _content!.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Attendance Monitoring", 20, _ink, true),
                new HorizontalStackLayout { Spacing = 10, Children = { Entry("Member name", "Mark Reyes"), checkIn } },
                _activityStatus,
                Table(new[] { "Member", "Time In", "Status", "Handled By" }, new[]
                {
                    new[] { "Alyssa Cruz", "8:10 AM", "Inside", "Staff" },
                    new[] { "Mark Reyes", "9:04 AM", "Inside", "Staff" },
                    new[] { "Nina Santos", "Yesterday", "Completed", "Admin" }
                })
            }
        }));
    }

    private void RenderTrainers()
    {
        _content!.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Trainer Management", 20, _ink, true),
                Table(new[] { "Trainer", "Specialization", "Availability", "Assigned" }, new[]
                {
                    new[] { "Coach Ben", "Strength", "Mon-Fri AM", "12 members" },
                    new[] { "Coach Mira", "Cardio", "Tue-Sat PM", "8 members" },
                    new[] { "Coach Leo", "Bodybuilding", "Weekends", "5 members" }
                })
            }
        }));
    }

    private void RenderWorkoutPlans()
    {
        _content!.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Workout Plan Assignments", 20, _ink, true),
                Table(new[] { "Plan", "Assigned To", "Focus", "Exercises" }, new[]
                {
                    new[] { "Beginner Strength", "Alyssa Cruz", "Full body", "6" },
                    new[] { "Weight Loss 4 Weeks", "Nina Santos", "Cardio", "8" },
                    new[] { "Hypertrophy Split", "Template", "Muscle gain", "10" }
                })
            }
        }));
    }

    private void RenderEquipment()
    {
        _content!.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Equipment Inventory", 20, _ink, true),
                Table(new[] { "Equipment", "Category", "Condition", "Next Maintenance" }, new[]
                {
                    new[] { "Treadmill 01", "Cardio", "Good", "May 12, 2026" },
                    new[] { "Bench Press A", "Strength", "Excellent", "Jun 03, 2026" },
                    new[] { "Cable Machine", "Strength", "Maintenance", "Overdue" }
                })
            }
        }));
    }

    private void RenderReports()
    {
        _content!.Children.Add(Card(new VerticalStackLayout
        {
            Spacing = 14,
            Children =
            {
                Label("Reports and KPIs", 20, _ink, true),
                Label("Reports are currently shown as KPI cards and tables. Export and database-backed filters are still ongoing.", 13, _muted),
                Table(new[] { "Metric", "This Month", "Notes", "Status" }, new[]
                {
                    new[] { "New members", "18", "Up from last month", "Working" },
                    new[] { "Renewals", "41", "Manual validation", "Ongoing" },
                    new[] { "Attendance visits", "642", "Based on local demo data", "Working" },
                    new[] { "Maintenance items", "3", "Needs alert rules", "Ongoing" }
                })
            }
        }));
    }

    private Grid Metric(string title, string value, string note, string background, string foreground, int column)
    {
        var card = new Grid
        {
            Padding = new Thickness(16),
            BackgroundColor = Color.FromArgb(background),
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto)
            }
        };
        card.Add(Label(title, 13, Color.FromArgb(foreground), true), 0, 0);
        card.Add(Label(value, 28, _ink, true), 0, 1);
        card.Add(Label(note, 12, _muted), 0, 2);
        Grid.SetColumn(card, column);
        return card;
    }

    private View Table(string[] headers, string[][] rows)
    {
        var grid = new Grid
        {
            RowSpacing = 0,
            ColumnSpacing = 0,
            BackgroundColor = _line
        };

        foreach (var _ in headers)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var c = 0; c < headers.Length; c++)
        {
            grid.Add(Cell(headers[c], true), c, 0);
        }

        for (var r = 0; r < rows.Length; r++)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            for (var c = 0; c < rows[r].Length; c++)
            {
                grid.Add(Cell(rows[r][c], false), c, r + 1);
            }
        }

        return grid;
    }

    private Border Cell(string text, bool header)
    {
        return new Border
        {
            Stroke = _line,
            StrokeThickness = 0.5,
            BackgroundColor = header ? Color.FromArgb("#F2F4F7") : Colors.White,
            Padding = new Thickness(12, 10),
            Content = Label(text, header ? 13 : 12, header ? _ink : _muted, header)
        };
    }

    private View ProgressLine(string label, double progress)
    {
        return new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition(GridLength.Star),
                        new ColumnDefinition(GridLength.Auto)
                    },
                    Children =
                    {
                        Label(label, 13, _ink, true).At(0),
                        Label(Math.Round(progress * 100) + "%", 12, _muted).At(1)
                    }
                },
                new ProgressBar { Progress = progress, ProgressColor = _accent }
            }
        };
    }

    private Button PrimaryButton(string text) => new()
    {
        Text = text,
        BackgroundColor = _accent,
        TextColor = Colors.White,
        CornerRadius = 6,
        Padding = new Thickness(16, 10),
        MinimumHeightRequest = 42
    };

    private Button SecondaryButton(string text) => new()
    {
        Text = text,
        BackgroundColor = Colors.White,
        TextColor = _accentDark,
        BorderColor = _accent,
        BorderWidth = 1,
        CornerRadius = 6,
        Padding = new Thickness(16, 10),
        MinimumHeightRequest = 42
    };

    private Entry Entry(string placeholder, string text = "") => new()
    {
        Placeholder = placeholder,
        Text = text,
        BackgroundColor = Colors.White,
        TextColor = _ink,
        PlaceholderColor = _muted,
        MinimumWidthRequest = 170
    };

    private View Field(string label, View input)
    {
        return new VerticalStackLayout
        {
            Spacing = 5,
            Children =
            {
                Label(label, 12, _muted, true),
                input
            }
        };
    }

    private Border Card(View content)
    {
        return new Border
        {
            BackgroundColor = _panel,
            Stroke = _line,
            StrokeThickness = 1,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(8) },
            Padding = new Thickness(20),
            Content = content
        };
    }

    private View Pill(string text, Color background, Color foreground)
    {
        return new Border
        {
            BackgroundColor = background,
            StrokeThickness = 0,
            StrokeShape = new RoundRectangle { CornerRadius = new CornerRadius(999) },
            Padding = new Thickness(12, 7),
            Content = Label(text, 12, foreground, true)
        };
    }

    private View Bullet(string text)
    {
        return new HorizontalStackLayout
        {
            Spacing = 8,
            Children =
            {
                Label("OK", 11, _accent, true),
                Label(text, 13, _muted)
            }
        };
    }

    private Label Label(string text, double size, Color color, bool bold = false)
    {
        return new Label
        {
            Text = text,
            FontSize = size,
            TextColor = color,
            FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
            LineBreakMode = LineBreakMode.WordWrap
        };
    }
}

internal static class LayoutExtensions
{
    public static T At<T>(this T view, int column, int row = 0) where T : View
    {
        Grid.SetColumn(view, column);
        Grid.SetRow(view, row);
        return view;
    }
}
