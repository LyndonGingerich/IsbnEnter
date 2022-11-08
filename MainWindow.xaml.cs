using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using CsvHelper;

namespace IsbnEnter;

public partial class MainWindow {
  private static readonly HttpClient HttpClient = new();

  public static readonly DependencyProperty IsbnStringProperty =
    DependencyProperty.Register(
      nameof(IsbnString), typeof(string), typeof(MainWindow), new PropertyMetadata(CheckIsbn));

  public static readonly DependencyProperty IsbnProperty =
    DependencyProperty.Register(
      nameof(Isbn), typeof(CheckedIsbn?), typeof(MainWindow), new PropertyMetadata());

  public MainWindow() {
    InitializeComponent();
  }

  private string IsbnString {
    get => (string)GetValue(IsbnStringProperty);
    set => SetValue(IsbnStringProperty, value);
  }

  private CheckedIsbn? Isbn {
    get => (CheckedIsbn?)GetValue(IsbnProperty);
    set => SetValue(IsbnProperty, value);
  }

  private static async void CheckIsbn(DependencyObject d, DependencyPropertyChangedEventArgs e) {
    var window = (MainWindow)d;
    var isbnString = (string)e.NewValue;
    if (CheckedIsbn.Create(isbnString) is not { } isbn) return;
    if (await ParseJson(isbn) is { } parsedJson) {
      window.Isbn = isbn;
      window.TitleText.Text = parsedJson.Title;
    }
    else {
      window.Isbn = null;
      window.TitleText.Text = "";
    }
  }

  private static async Task<ParsedJson?> ParseJson(CheckedIsbn isbn) {
    var url = "https://www.googleapis.com/books/v1/volumes?q=isbn:" + isbn.Value;
    var json = await HttpClient.GetStringAsync(url);
    return
      JsonNode.Parse(json) is { } node
        ? new ParsedJson((string)((dynamic)node)["items"][0]["volumeInfo"]["title"])
        : null;
  }

  private void WriteCsv(object sender, KeyEventArgs e) {
    if (e.Key != Key.Enter) return;
    if (!int.TryParse(CallNumberText.Text, out var callNumber)) return;
    if (Isbn is not { } isbn) return;

    var title = TitleText.Text;
    if (title == "") return;

    using (var writer = new StreamWriter("records.csv")) {
      using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);
      var entry = new CsvEntry { CallNumber = callNumber, Isbn = isbn.Value };
      csv.WriteRecords(new List<CsvEntry> { entry });
    }

    IsbnText.Text = "";
    CallNumberText.Text = "";
  }
}

internal record struct CheckedIsbn {
  private CheckedIsbn(string isbn) {
    Value = isbn;
  }

  public string Value { get; }

  public static CheckedIsbn? Create(string isbn) {
    bool IsFormattedAsIsbn(string s) {
      char[] validChars = { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', 'X' };
      return s.AsEnumerable().All(c => validChars.Contains(c)) && s.Length is 10 or 13;
    }

    return IsFormattedAsIsbn(isbn) ? new CheckedIsbn(isbn) : null;
  }
}

internal record struct ParsedJson(string Title) {
  public string Title { get; } = Title;
}

internal record struct CsvEntry {
  public int CallNumber { get; init; }
  public string Isbn { get; init; }
}

