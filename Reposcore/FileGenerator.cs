using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Drawing;
using ConsoleTables;
using ScottPlot;
using ScottPlot.Plottables;
using ScottPlot.TickGenerators;
using Alignment = ScottPlot.Alignment;
using Color = System.Drawing.Color;

public class FileGenerator
{
    private readonly Dictionary<string, UserScore> _scores;
    private readonly string _repoName;
    private readonly string _folderPath;

    public FileGenerator(Dictionary<string, UserScore> repoScores, string repoName, string folderPath)
    {
        _scores = repoScores;
        _repoName = repoName;
        _folderPath = Path.Combine(folderPath, repoName);

        try
        {
            Directory.CreateDirectory(_folderPath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❗ 결과 디렉토리 생성에 실패했습니다. (경로: {_folderPath})");
            Console.WriteLine($"→ 디스크 권한이나 경로 오류를 확인하세요: {ex.Message}");
            Environment.Exit(1);
        }
    }

    double sumOfPR
    {
        get
        {
            return _scores.Sum(pair => pair.Value.PR_doc + pair.Value.PR_fb + pair.Value.PR_typo);
        }        
    }

    double sumOfIs
    {
        get { return _scores.Sum(pair => pair.Value.IS_doc + pair.Value.IS_fb); }
    }

    public void GenerateCsv()
    {
        // 경로 설정
        string filePath = Path.Combine(_folderPath, $"{_repoName}.csv");
        using StreamWriter writer = new StreamWriter(filePath);


        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        // 파일에 "# ..." 로 주석을 작성하면 CSV 주석으로 활용된다.
        writer.WriteLine($"# Generated: {timestamp}");
        writer.WriteLine("# 점수 계산 기준: PR_fb*3, PR_doc*2, PR_typo*1, IS_fb*2, IS_doc*1");
        // CSV 헤더
        writer.WriteLine("User,f/b_PR,doc_PR,typo,f/b_issue,doc_issue,PR_rate,IS_rate,total");

        // 내용 작성
        foreach (var (id, scores) in _scores.OrderByDescending(x => x.Value.total))
        {
            double prRate = (sumOfPR > 0) ? (scores.PR_doc + scores.PR_fb + scores.PR_typo) / sumOfPR * 100 : 0.0;
    double isRate = (sumOfIs > 0) ? (scores.IS_doc + scores.IS_fb) / sumOfIs * 100 : 0.0;
            string line =
                $"{id},{scores.PR_fb},{scores.PR_doc},{scores.PR_typo},{scores.IS_fb},{scores.IS_doc},{prRate:F1},{isRate:F1},{scores.total}";
            writer.WriteLine(line);
        }

        Console.WriteLine($"{filePath} 생성됨");
    }
    public void GenerateTable()
    {
        // 출력할 파일 경로
        string filePath = Path.Combine(_folderPath, $"{_repoName}1.txt");

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");

        // 테이블 생성
        var headers = "UserId,f/b_PR,doc_PR,typo,f/b_issue,doc_issue,PR_rate,IS_rate,total".Split(',');

        // 각 칸의 너비 계산 (오른쪽 정렬을 위해 사용)
        int[] colWidths = headers.Select(h => h.Length).ToArray();

        var table = new ConsoleTable(headers);

        // 내용 작성
        foreach (var (id, scores) in _scores.OrderByDescending(x => x.Value.total))
        {
            double prRate = (sumOfPR > 0) ? (scores.PR_doc + scores.PR_fb + scores.PR_typo) / sumOfPR * 100 : 0.0;
            double isRate = (sumOfIs > 0) ? (scores.IS_doc + scores.IS_fb) / sumOfIs * 100 : 0.0;
            table.AddRow(
                id.PadRight(colWidths[0]), // 글자는 왼쪽 정렬                   
                scores.PR_fb.ToString().PadLeft(colWidths[1]), // 숫자는 오른쪽 정렬
                scores.PR_doc.ToString().PadLeft(colWidths[2]),
                scores.PR_typo.ToString().PadLeft(colWidths[3]),
                scores.IS_fb.ToString().PadLeft(colWidths[4]),
                scores.IS_doc.ToString().PadLeft(colWidths[5]),
                $"{prRate:F1}".PadLeft(colWidths[6]),
                $"{isRate:F1}".PadLeft(colWidths[7]),
                scores.total.ToString().PadLeft(colWidths[8])
            );
        }
        
        // 점수 기준 주석과 테이블 같이 출력
        var tableText = table.ToMinimalString();
        var content = $"# Generated: {timestamp}"
                    + Environment.NewLine
                    + "# 점수 계산 기준: PR_fb*3, PR_doc*2, PR_typo*1, IS_fb*2, IS_doc*1"
                    + Environment.NewLine
                    + tableText;
        File.WriteAllText(filePath, content);
        Console.WriteLine($"{filePath} 생성됨");
    }

    public void GenerateChart()
    {
        var labels = new List<string>();
        var values = new List<double>();

        // total 점수 내림차순 정렬
        var sorted = _scores.OrderByDescending(x => x.Value.total).ToList();
        var rankList = new List<(int Rank, string User, double Score)>();
        int rank = 1;
        int count = 1;
        double? prevScore = null;

        foreach (var pair in sorted)
        {
            if (prevScore != null && pair.Value.total != prevScore)
            {
                rank = count;
            }
            rankList.Add((rank, pair.Key, pair.Value.total));
            prevScore = pair.Value.total;
            count++;
        }

        // 차트는 오름차순으로 표시
        foreach (var item in rankList.OrderBy(x => x.Score))
        {
            string suffix = item.Rank switch
            {
                1 => "st",
                2 => "nd",
                3 => "rd",
                _ => "th"
            };
            labels.Add($"{item.User} ({item.Rank}{suffix})");
            values.Add(item.Score);
        }

        string[] names = labels.ToArray();
        double[] scores = values.ToArray();
        
        // ✅ 간격 조절된 Position
        double spacing = 10; // 막대 간격
        double[] positions = Enumerable.Range(0, names.Length)
                                    .Select(i => i * spacing)
                                    .ToArray();

        // Bar 데이터 생성
        var plt = new ScottPlot.Plot();
        var bars = new List<Bar>();
        for (int i = 0; i < scores.Length; i++)
        {
            bars.Add(new Bar
            {
                Position = positions[i],
                Value = scores[i],
                FillColor = Colors.SteelBlue,
                Orientation = Orientation.Horizontal,
                Size = 5,
            });

            double textX = scores[i] + scores.Max() * 0.01;
            double textY = positions[i];

            var txt = plt.Add.Text($"{scores[i]:F1}", textX, textY);
            txt.Alignment = Alignment.MiddleLeft;
        }

        var barPlot = plt.Add.Bars(bars);

        plt.Axes.Left.TickGenerator = new NumericManual(positions, names);
        double avgScore = _scores.Count > 0 ? _scores.Average(x => (double)x.Value.total) : 0.0;
        double maxScore = _scores.Count > 0 ? _scores.Max(x => x.Value.total) : 0.0;
        double minScore = _scores.Count > 0 ? _scores.Min(x => x.Value.total) : 0.0;
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
        plt.Title($"Repo: {_repoName} Date: {timestamp} Avg: {avgScore:F1} Max: {maxScore} Min: {minScore}");
        plt.XLabel("Total Score");
        plt.YLabel("User");

        // x축 범위 설정
        plt.Axes.Bottom.Min = 0;
        plt.Axes.Bottom.Max = scores.Max() * 1.1; // 최대값의 110%까지 표시

        string outputPath = Path.Combine(_folderPath, $"{_repoName}_chart.png");
        plt.SavePng(outputPath, 1080, 1920);
        Console.WriteLine($"✅ 차트 생성 완료: {outputPath}");
    }

    public void GenerateStateSummary(RepoStateSummary summary)
    {
        string filePath = Path.Combine(_folderPath, $"{_repoName}_state.txt");
        using StreamWriter writer = new StreamWriter(filePath);
        writer.WriteLine($"Merged PR: {summary.MergedPR}");
        writer.WriteLine($"Unmerged PR: {summary.UnmergedPR}");
        writer.WriteLine($"Open Issue: {summary.OpenIssue}");
        writer.WriteLine($"Closed Issue: {summary.ClosedIssue}");
        Console.WriteLine($"{filePath} 생성됨");
    }


}
