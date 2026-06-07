using System.IO;
using System.Collections.Generic;
using System.Linq;
using Gtk;
using Cairo;
using Newtonsoft.Json;



/// The project Is a budgeting application.It has three classes ;each covering different aspects of the code;
/// thie first is a datatype for transactions,the second a class to store and retreive data and draw spesific 
/// aspects of it; and the third to generate the UI which uses GTK and cairo.The data are stored in two different files; 
/// one for the limits of a given categories spending; and one for the actual records of transactions tha thave happened.
/// The Application provides the user with the abilities of adding and deleting transactions; to have their data visualized
///  by pie and bar charts; to filter said transactions and set limits for them.It also warns against going overbudget when
///  a spesific limit is exceeded.



//This is the datatype class for transactions.
public class Transaction
{
        public Guid Id { get; set; } = Guid.NewGuid();
        public DateTime Date { get; set; } = DateTime.Today;
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Category { get; set; } = string.Empty;
        public bool IsIncome { get; set; }
}

    


//This is the data storage class for storign said data and retreiving it.It loads said data from json files. 
class DataStorage
{

    const string TransactionsFile = "transactions.json";
    const string LimitsFile = "limits.json";

    public List<Transaction> Transactions { get; private set; } = new List<Transaction>();
    public Dictionary<string, decimal> SpendingLimits { get; private set; } = new Dictionary<string, decimal>();

    public void Load()
    {
        if (File.Exists(TransactionsFile))
        {
            string json = File.ReadAllText(TransactionsFile);
            Transactions = JsonConvert.DeserializeObject<List<Transaction>>(json) ?? new List<Transaction>();
        }
        if (File.Exists(LimitsFile))
        {
            string json = File.ReadAllText(LimitsFile);
            SpendingLimits = JsonConvert.DeserializeObject<Dictionary<string, decimal>>(json) ?? new Dictionary<string, decimal>();
        }
    }
    public void Save()
    {
        File.WriteAllText(TransactionsFile, JsonConvert.SerializeObject(Transactions, Formatting.Indented));
        File.WriteAllText(LimitsFile, JsonConvert.SerializeObject(SpendingLimits, Formatting.Indented));
    }


    public void Add(Transaction t)
    {
        Transactions.Add(t);
        Save();
    }
    public void Delete(Guid id)
    {
        Transactions.RemoveAll(t => t.Id == id);
        Save();
    }
    public void SetLimit(string category, decimal limit)
    {
        SpendingLimits[category] = limit;
        Save();
    }

   




    public IEnumerable<Transaction> GetFiltered(int? month = null, int? year = null, string category = null)
    {
        IEnumerable<Transaction> result = Transactions;

        if (month!= null) result = result.Where(t => t.Date.Month == month);
        if (year!= null) result = result.Where(t => t.Date.Year== year);
        if (category!= null) result = result.Where(t => t.Category== category);

        return result;
    
    }
    public decimal GetBalance()
    {
        return Transactions.Sum(t => t.IsIncome ? t.Amount : -t.Amount);
    }
    public decimal GetTotalIncome()
    {
        return Transactions.Where(t => t.IsIncome).Sum(t => t.Amount);
    }
    public decimal GetTotalExpenses()
    {
       return Transactions.Where(t => !t.IsIncome).Sum(t => t.Amount);
    }

    public Dictionary<string, decimal> GetTotalByCategory()
    {
        return Transactions.Where(t => !t.IsIncome).GroupBy(t => t.Category).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }
    public Dictionary<string, decimal> GetMonthlyTotals()
    {
        return Transactions.Where(t => !t.IsIncome).GroupBy(t => t.Date.ToString("yyyy-MM")).OrderBy(g => g.Key).ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }
    
}


//And finally this is the visual UI class.
//It uses GTK-Cairo-GTKSharp to show the user the data in a nice fasion.

public class MainWindow : Gtk.Window
{
    private DataStorage dataStore = new DataStorage();


    private ListStore listStore;
    private TreeView treeView;
    private ComboBoxText monthFilter;
    private ComboBoxText categoryFilter;
    private Label balanceLabel;
    private Label incomeLabel;
    private Label expenseLabel;
    private DrawingArea pieView;
    private DrawingArea barView;
    private Statusbar statusBar;


    public MainWindow() : base("Budgeting App")
    {
        SetDefaultSize(1000, 600);
        DeleteEvent += (o, e) => Application.Quit();

        dataStore.Load();
        BuildUI();
        RefreshAll();
    }

    public static void Main()
    {
        Application.Init();
        new MainWindow();
        Application.Run();
    }
 

    private void BuildUI()
    {
        VBox vbox = new VBox(false, 0);
        Add(vbox);

        HBox toolbar = new HBox(false, 6);
        toolbar.BorderWidth = 8;

        monthFilter = new ComboBoxText();
        monthFilter.AppendText("All Months");

        for (int i = 1; i <= 12; i++) {
            monthFilter.AppendText(new DateTime(2000, i, 1).ToString("MMMM"));
        }

        monthFilter.Active = 0;
        monthFilter.Changed += OnFilterChanged;
        categoryFilter = new ComboBoxText();
        categoryFilter.AppendText("All Categories");

        foreach (string cat in GetCategories()){
            categoryFilter.AppendText(cat);
        }

        categoryFilter.Active = 0;
        categoryFilter.Changed += OnFilterChanged;

        Button addButton = new Button("Add Transaction");
        addButton.Clicked += OnAddClicked;
        Button limitButton = new Button("Set Limits");
        limitButton.Clicked += OnLimitsClicked;

        toolbar.PackStart(new Label("Month:"),false, false, 0);
        toolbar.PackStart(monthFilter,false, false, 0);
        toolbar.PackStart(new Label("Category:"),false, false, 4);
        toolbar.PackStart(categoryFilter,false, false, 0);
        toolbar.PackEnd(limitButton,false, false, 0);
        toolbar.PackEnd(addButton,false, false, 0);

        vbox.PackStart(toolbar,         false, false, 0);
        vbox.PackStart(new HSeparator(), false, false, 0);

        
        HPaned paned = new HPaned();
        vbox.PackStart(paned, true, true, 0);

        VBox leftBox = new VBox(false, 4);
        leftBox.BorderWidth = 8;

        listStore = new ListStore(typeof(string),typeof(string),typeof(string),typeof(string),typeof(string),typeof(string));

        treeView = new TreeView(listStore);
        treeView.AppendColumn("Date", new CellRendererText(), "text", 0);
        treeView.AppendColumn("Description",new CellRendererText(), "text", 1);
        treeView.AppendColumn("Category",new CellRendererText(), "text", 2);
        treeView.AppendColumn("Amount",new CellRendererText(), "text", 3);
        treeView.AppendColumn("Type", new CellRendererText(), "text", 4);

        ScrolledWindow scrolled = new ScrolledWindow();
        scrolled.SetSizeRequest(480, 300);
        scrolled.Add(treeView);

        leftBox.PackStart(scrolled, true, true, 0);
        Button deleteButton = new Button("Delete Selected");
        deleteButton.Clicked += OnDeleteClicked;
        leftBox.PackStart(deleteButton, false, false, 0);

        incomeLabel  = new Label("Total Income: —");
        expenseLabel = new Label("Total Expenses: —");
        balanceLabel = new Label("Balance: —");

        leftBox.PackStart(incomeLabel,false, false, 0);
        leftBox.PackStart(expenseLabel,false, false, 0);
        leftBox.PackStart(balanceLabel,false, false, 2);

        paned.Pack1(leftBox,true,false);

    
        Notebook notebook = new Notebook();

        pieView = new DrawingArea();
        pieView.SetSizeRequest(400, 300);
        pieView.Drawn += DrawPieChart;
        notebook.AppendPage(pieView, new Label("By Category"));

        barView = new DrawingArea();
        barView.SetSizeRequest(400, 300);
        barView.Drawn += DrawBarChart;
        notebook.AppendPage(barView, new Label("By Month"));

        VBox rightBox = new VBox(false,0);
        rightBox.BorderWidth = 8;
        rightBox.PackStart(notebook,true,true,0);
        paned.Pack2(rightBox,true,false);

      
        statusBar = new Statusbar();
        vbox.PackStart(statusBar,false,false,0);

        ShowAll();
    }

  

    private void RefreshAll()
    {
        RefreshTreeView();
        RefreshSummary();
        RefreshCharts();
        RefreshLimitWarnings();
    }

    private void RefreshTreeView()
    {
        listStore.Clear();

        int? month = monthFilter.Active > 0 ? monthFilter.Active : (int?)null;
        int? year  = DateTime.Today.Year;
        string cat = categoryFilter.Active > 0 ? categoryFilter.ActiveText : null;

        foreach (Transaction t in dataStore.GetFiltered(month, year, cat))
        {
            listStore.AppendValues(t.Date.ToString("yyyy-MM-dd"),t.Description,t.Category,t.Amount.ToString("F2"),t.IsIncome ? "Income" : "Expense",t.Id.ToString());
        }
    }

    private void RefreshSummary()
    {
        balanceLabel.Text= $"Balance:        {dataStore.GetBalance():F2}";
        incomeLabel.Text= $"Total Income:   {dataStore.GetTotalIncome():F2}";
        expenseLabel.Text= $"Total Expenses: {dataStore.GetTotalExpenses():F2}";
    }

    private void RefreshCharts()
    {
        pieView.QueueDraw();
        barView.QueueDraw();
    }




    private void DrawPieChart(object sender, DrawnArgs args)
    {
        Cairo.Context cr = args.Cr;
        Dictionary<string, decimal> data = dataStore.GetTotalByCategory();

        DrawingArea widget = (DrawingArea)sender;
        double cx = widget.AllocatedWidth / 2.0;
        double cy = widget.AllocatedHeight / 2.0;
        double radius = Math.Min(cx, cy) - 20;

        if (data.Count == 0) return;

        double total = (double)data.Values.Sum();
        double[] colors = { 0.9, 0.4, 0.2, 0.3, 0.7, 0.6, 0.5, 0.8, 0.1, 0.95 };
        double angle = -Math.PI / 2;
        int i = 0;

        foreach (KeyValuePair<string, decimal> kvp in data)
        {
            double slice = (double)kvp.Value / total * 2 * Math.PI;
            double hue = colors[i % colors.Length];

            cr.SetSourceRGB(hue, 0.6, 1.0 - hue);
            cr.MoveTo(cx, cy);
            cr.Arc(cx, cy, radius, angle, angle + slice);
            cr.ClosePath();

            cr.FillPreserve();
            cr.SetSourceRGB(1, 1, 1);
            cr.LineWidth = 1.5;
            cr.Stroke();

            double midAngle = angle + slice /2;
            double lx = cx + radius * 0.65 * Math.Cos(midAngle);
            double ly = cy + radius * 0.65 * Math.Sin(midAngle);
            cr.SetSourceRGB(1, 1, 1);
            cr.MoveTo(lx - 20, ly);
            cr.ShowText(kvp.Key.Length > 5 ? kvp.Key.Substring(0, 5) : kvp.Key);

            angle += slice;
            i++;
        }
    }


    private void DrawBarChart(object sender, DrawnArgs args)
    {
        Cairo.Context cr = args.Cr;
        Dictionary<string, decimal> data = dataStore.GetMonthlyTotals();
        DrawingArea widget = (DrawingArea)sender;

        double w = widget.AllocatedWidth;
        double h = widget.AllocatedHeight;
        double padding = 40;

        if (data.Count == 0) return;

        double maxVal = (double)data.Values.Max();
        double barWidth = (w - padding * 2) / data.Count - 4;
        double x = padding;
        int i = 0;

        foreach (KeyValuePair<string, decimal> kvp in data)
        {
            double barH = ((double)kvp.Value / maxVal) * (h - padding * 2);
            double y = h - padding - barH;

            cr.SetSourceRGB(0.3, 0.6, 0.9);
            cr.Rectangle(x, y, barWidth, barH);
            cr.Fill();
            cr.SetSourceRGB(0.2, 0.2, 0.2);
            cr.MoveTo(x, h - padding + 12);
            cr.ShowText(kvp.Key.Length > 7 ? kvp.Key.Substring(5) : kvp.Key);

            x += barWidth + 4;
            i++;
        }


        cr.SetSourceRGB(0.5, 0.5, 0.5);
        cr.MoveTo(padding, h - padding);
        cr.LineTo(w - padding, h - padding);
        cr.LineWidth = 1;
        cr.Stroke();
    }

    private void RefreshLimitWarnings()
    {
        List<string> warnings   = new List<string>();
        Dictionary<string, decimal> byCategory = dataStore.GetTotalByCategory();

        foreach (KeyValuePair<string, decimal> kvp in dataStore.SpendingLimits)
        {
            if (byCategory.TryGetValue(kvp.Key, out decimal spent) && spent > kvp.Value){
                warnings.Add($"{kvp.Key}: spent {spent:F2}, limit {kvp.Value:F2}");
            }
        }

        statusBar.RemoveAll(0);
        if (warnings.Count > 0){
            statusBar.Push(0, "Over limit: " + string.Join("  |  ", warnings));
        }
    }

  

    private void OnFilterChanged(object sender, EventArgs e) => RefreshAll();

    private void OnAddClicked(object sender, EventArgs e)
    {
        using Dialog dialog = new Dialog("Add Transaction", this, DialogFlags.Modal);
        dialog.SetDefaultSize(340, 300);

        var content = dialog.ContentArea;
        content.BorderWidth = 12;
        content.Spacing = 6;

        Entry descEntry = new Entry();
        SpinButton amountSpin = new SpinButton(0.01, 1_000_000, 0.01);
        ComboBoxText categoryCombo = new ComboBoxText();
        foreach (string cat in GetCategories()){
            categoryCombo.AppendText(cat);
        }
        categoryCombo.Active = 0;

        RadioButton incomeRadio = new RadioButton("Income");
        RadioButton expenseRadio = new RadioButton(incomeRadio, "Expense");
        expenseRadio.Active = true;

        HBox typeBox = new HBox(false,8);
        typeBox.PackStart(incomeRadio,false,false,0);
        typeBox.PackStart(expenseRadio,false,false,0);

        content.PackStart(new Label("Description:"),false,false,0);
        content.PackStart(descEntry,false, false, 0);
        content.PackStart(new Label("Amount:"), false,false, 0);
        content.PackStart(amountSpin,false,false,0);


        content.PackStart(new Label("Category:"),false,false,0);
        content.PackStart(categoryCombo,false,false,0);
        content.PackStart(new Label("Type:"),false,false,0);
        content.PackStart(typeBox,false, false, 0);


        dialog.AddButton("Cancel", ResponseType.Cancel);
        dialog.AddButton("Add", ResponseType.Accept);
        dialog.ShowAll();

        if (dialog.Run() == (int)ResponseType.Accept)
        {
            dataStore.Add(new Transaction{Description = descEntry.Text, Amount = (decimal)amountSpin.Value, 
            Category= categoryCombo.ActiveText, IsIncome = incomeRadio.Active});

            RefreshAll();
        }
    }

    private void OnDeleteClicked(object sender, EventArgs e)
    {
        if (!treeView.Selection.GetSelected(out TreeIter iter)){
            return;
        }

        string idStr = (string)listStore.GetValue(iter, 5);
        if (Guid.TryParse(idStr, out Guid id))
        {
            dataStore.Delete(id);

            RefreshAll();
        }
    }
 

    private void OnLimitsClicked(object sender, EventArgs e)
    {
        using Dialog dialog = new Dialog("Spending Limits", this, DialogFlags.Modal);
        dialog.SetDefaultSize(320, 280);

        var content = dialog.ContentArea;
        content.BorderWidth = 12;
        content.Spacing = 6;

        Dictionary<string, SpinButton> spins = new Dictionary<string, SpinButton>();

        foreach (string cat in GetCategories())
        {
            dataStore.SpendingLimits.TryGetValue(cat, out decimal current);
            SpinButton spin = new SpinButton(0, 100_000, 10) { Value = (double)current };
            spins[cat] = spin;

            HBox row = new HBox(false, 8);
            row.PackStart(new Label(cat + ":"), false, false, 0);
            row.PackEnd(spin,false, false, 0);
            content.PackStart(row, false, false, 0);
        }

        dialog.AddButton("Cancel", ResponseType.Cancel);
        dialog.AddButton("Save",ResponseType.Accept);
        dialog.ShowAll();

        if (dialog.Run() == (int)ResponseType.Accept)
        {
            foreach (KeyValuePair<string, SpinButton> kvp in spins){
                dataStore.SetLimit(kvp.Key, (decimal)kvp.Value.Value);
            }
            
            RefreshAll();
        }
    }



    private static string[] GetCategories() => new[]{"Food", "Rent", "Transport", "Utilities", "Health","Entertainment", "Clothing", "Savings", "Work", "Other"};


}