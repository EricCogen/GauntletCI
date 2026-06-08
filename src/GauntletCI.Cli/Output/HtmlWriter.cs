// SPDX-License-Identifier: Elastic-2.0
using System.Text;
using System.Text.Json;
using GauntletCI.Core.Model;
using GauntletCI.Core.Rules;

namespace GauntletCI.Cli.Output;

public static class HtmlWriter
{
    public static void Write(EvaluationResult result)
    {
        var jsonResult = new
        {
            result.CommitSha,
            result.HasFindings,
            Findings = result.Findings.Select(f => new
            {
                f.RuleId,
                f.RuleName,
                f.Summary,
                f.Evidence,
                f.WhyItMatters,
                f.SuggestedAction,
                Confidence = NormalizeConfidence(f.Confidence),
                f.Severity,
                f.FilePath,
                f.Line,
                f.CodeSnippet,
            }).ToList(),
            result.RulesEvaluated,
            RuleMetrics = result.RuleMetrics.Select(m => new
            {
                m.RuleId,
                m.DurationMs,
                m.Outcome,
                FindingCount = result.Findings.Count(f => f.RuleId == m.RuleId),
            }).ToList(),
            result.FileStatistics,
        };

        var json = JsonSerializer.Serialize(jsonResult, new JsonSerializerOptions { WriteIndented = true });
        var html = GenerateHtml(json);
        Console.WriteLine(html);
    }

    private static double NormalizeConfidence(Confidence confidence)
    {
        return confidence switch
        {
            Confidence.Low => 0.33,
            Confidence.Medium => 0.66,
            Confidence.High => 0.95,
            _ => 0.0,
        };
    }

    private static string GenerateHtml(string jsonData)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset=\"UTF-8\">");
        sb.AppendLine("    <meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">");
        sb.AppendLine("    <title>GauntletCI Analysis Report</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine(GetCss());
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class=\"container\">");
        sb.AppendLine("        <div class=\"header\">");
        sb.AppendLine("            <div style=\"display: flex; align-items: center; gap: 15px; margin-bottom: 10px;\">");
        sb.AppendLine("                <svg width=\"60\" height=\"60\" viewBox=\"0 0 100 124\" fill=\"none\" xmlns=\"http://www.w3.org/2000/svg\">");
        sb.AppendLine("                    <defs>");
        sb.AppendLine("                        <linearGradient id=\"border-grad\" x1=\"0\" y1=\"0\" x2=\"100\" y2=\"124\" gradientUnits=\"userSpaceOnUse\">");
        sb.AppendLine("                            <stop offset=\"0%\" stop-color=\"#6BFAFF\"/>");
        sb.AppendLine("                            <stop offset=\"50%\" stop-color=\"#00AAFF\"/>");
        sb.AppendLine("                            <stop offset=\"100%\" stop-color=\"#0044CC\"/>");
        sb.AppendLine("                        </linearGradient>");
        sb.AppendLine("                    </defs>");
        sb.AppendLine("                    <path d=\"M50 3 L97 16 L97 68 C97 99 50 121 50 121 C50 121 3 99 3 68 L3 16 Z\" fill=\"url(#border-grad)\"/>");
        sb.AppendLine("                    <path d=\"M50 12 L88 23 L88 68 C88 94 50 113 50 113 C50 113 12 94 12 68 L12 23 Z\" fill=\"#091827\"/>");
        sb.AppendLine("                    <rect x=\"23\" y=\"40\" width=\"54\" height=\"9\" rx=\"2\" fill=\"#00E8FF\"/>");
        sb.AppendLine("                    <rect x=\"23\" y=\"55\" width=\"30\" height=\"8\" rx=\"2\" fill=\"#00E8FF\"/>");
        sb.AppendLine("                    <rect x=\"58\" y=\"55\" width=\"20\" height=\"8\" rx=\"2\" fill=\"#00C8FF\"/>");
        sb.AppendLine("                    <rect x=\"23\" y=\"69\" width=\"44\" height=\"8\" rx=\"2\" fill=\"#00E0FF\"/>");
        sb.AppendLine("                    <rect x=\"23\" y=\"84\" width=\"9\" height=\"9\" rx=\"2\" fill=\"#00D8FF\"/>");
        sb.AppendLine("                    <rect x=\"36\" y=\"84\" width=\"9\" height=\"9\" rx=\"2\" fill=\"#0080EE\"/>");
        sb.AppendLine("                </svg>");
        sb.AppendLine("                <div>");
        sb.AppendLine("                    <h1>GauntletCI Analysis Report</h1>");
        sb.AppendLine("                    <p>Pre-commit risk assessment and code quality analysis</p>");
        sb.AppendLine("                </div>");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"metadata\" id=\"metadata\">");
        sb.AppendLine("            <!-- Populated by JavaScript -->");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"findings-section\">");
        sb.AppendLine("            <h2 class=\"section-title\">Findings Overview</h2>");
        sb.AppendLine("            <div class=\"summary-section\" id=\"summary\">");
        sb.AppendLine("                <!-- Populated by JavaScript -->");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"severity-filter\">");
        sb.AppendLine("                <button class=\"filter-btn\" onclick=\"filterBySeverity(1)\" id=\"btn-severity-1\">🔴 Block</button>");
        sb.AppendLine("                <button class=\"filter-btn\" onclick=\"filterBySeverity(2)\" id=\"btn-severity-2\">🟠 Warning</button>");
        sb.AppendLine("                <button class=\"filter-btn\" onclick=\"filterBySeverity(3)\" id=\"btn-severity-3\">🔵 Info</button>");
        sb.AppendLine("                <button class=\"filter-btn active\" onclick=\"filterBySeverity('all')\">All Findings</button>");
        sb.AppendLine("            </div>");
        sb.AppendLine("            <div class=\"findings-grid\" id=\"findings\">");
        sb.AppendLine("                <!-- Populated by JavaScript -->");
        sb.AppendLine("            </div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <div class=\"footer\" id=\"footer\">");
        sb.AppendLine("            <!-- Populated by JavaScript -->");
        sb.AppendLine("        </div>");
        sb.AppendLine("    </div>");
        sb.AppendLine();
        sb.AppendLine("    <script>");
        sb.AppendLine(GetJavaScript());
        sb.AppendLine("    </script>");
        sb.AppendLine();
        sb.AppendLine("    <script type=\"application/json\" id=\"analysis-data\">");
        sb.AppendLine(jsonData);
        sb.AppendLine("    </script>");
        sb.AppendLine();
        sb.AppendLine("    <script>");
        sb.AppendLine("        document.addEventListener('DOMContentLoaded', function() {");
        sb.AppendLine("            const dataElement = document.getElementById('analysis-data');");
        sb.AppendLine("            if (dataElement) {");
        sb.AppendLine("                try {");
        sb.AppendLine("                    const data = JSON.parse(dataElement.textContent);");
        sb.AppendLine("                    initializeReport(data);");
        sb.AppendLine("                } catch (e) {");
        sb.AppendLine("                    console.error('Failed to parse JSON:', e);");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");
        return sb.ToString();
    }

    private static string GetCss()
    {
        return @"* {
            margin: 0;
            padding: 0;
            box-sizing: border-box;
        }

        body {
            font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
            background: linear-gradient(135deg, #f5f7fa 0%, #c3cfe2 100%);
            color: #2c3e50;
            line-height: 1.6;
            min-height: 100vh;
            padding: 20px;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
            background: white;
            border-radius: 12px;
            box-shadow: 0 20px 60px rgba(0, 0, 0, 0.15);
            overflow: hidden;
        }

        .header {
            background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
            color: white;
            padding: 40px;
            text-align: left;
        }

        .header h1 {
            font-size: 2.5em;
            margin-bottom: 5px;
            font-weight: 700;
        }

        .header p {
            font-size: 1.1em;
            opacity: 0.95;
        }

        .metadata {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 20px;
            padding: 30px 40px;
            background: #f8f9fa;
            border-bottom: 1px solid #e9ecef;
        }

        .stat-card {
            padding: 20px;
            background: white;
            border-radius: 8px;
            border-left: 4px solid #667eea;
        }

        .stat-label {
            font-size: 0.85em;
            color: #6c757d;
            text-transform: uppercase;
            letter-spacing: 1px;
            margin-bottom: 8px;
        }

        .stat-value {
            font-size: 2em;
            font-weight: 700;
            color: #667eea;
        }

        .findings-section {
            padding: 40px;
        }

        .section-title {
            font-size: 1.8em;
            font-weight: 700;
            margin-bottom: 30px;
            color: #2c3e50;
            border-bottom: 3px solid #667eea;
            padding-bottom: 15px;
        }

        .severity-filter {
            display: flex;
            gap: 10px;
            margin-bottom: 30px;
            flex-wrap: wrap;
        }

        .filter-btn {
            padding: 10px 20px;
            border: 2px solid #ddd;
            background: white;
            border-radius: 6px;
            cursor: pointer;
            font-weight: 600;
            transition: all 0.3s ease;
        }

        .filter-btn:hover {
            border-color: #667eea;
            color: #667eea;
        }

        .filter-btn.active {
            background: #667eea;
            color: white;
            border-color: #667eea;
        }

        .findings-grid {
            display: grid;
            gap: 20px;
        }

        .finding-card {
            border-left: 5px solid;
            border-radius: 8px;
            padding: 25px;
            background: #f8f9fa;
            transition: all 0.3s ease;
        }

        .finding-card:hover {
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.1);
            transform: translateY(-2px);
        }

        .finding-card.severity-1 {
            border-left-color: #dc3545;
            background: #fff5f5;
        }

        .finding-card.severity-2 {
            border-left-color: #fd7e14;
            background: #fffbf0;
        }

        .finding-card.severity-3 {
            border-left-color: #0dcaf0;
            background: #f0f8ff;
        }

        .finding-header {
            display: flex;
            justify-content: space-between;
            align-items: start;
            margin-bottom: 15px;
            gap: 15px;
        }

        .finding-title {
            flex: 1;
        }

        .rule-id {
            display: inline-block;
            background: #667eea;
            color: white;
            padding: 6px 12px;
            border-radius: 6px;
            font-weight: 700;
            font-size: 0.9em;
        }

        .rule-name {
            font-size: 1.3em;
            font-weight: 700;
            color: #2c3e50;
            margin-top: 8px;
        }

        .severity-badge {
            display: inline-block;
            padding: 8px 16px;
            border-radius: 6px;
            font-weight: 600;
            font-size: 0.9em;
            text-transform: uppercase;
            letter-spacing: 0.5px;
        }

        .severity-1 {
            background: #dc3545;
            color: white;
        }

        .severity-2 {
            background: #fd7e14;
            color: white;
        }

        .severity-3 {
            background: #0dcaf0;
            color: white;
        }

        .finding-content {
            margin-bottom: 20px;
        }

        .finding-section {
            margin-bottom: 15px;
        }

        .finding-label {
            font-weight: 700;
            color: #667eea;
            margin-bottom: 5px;
            font-size: 0.95em;
        }

        .finding-text {
            color: #495057;
            padding: 10px;
            background: white;
            border-radius: 6px;
            border-left: 3px solid #667eea;
        }

        .finding-meta {
            display: flex;
            gap: 20px;
            flex-wrap: wrap;
            padding-top: 15px;
            border-top: 1px solid #dee2e6;
            margin-top: 15px;
            font-size: 0.9em;
        }

        .meta-item {
            display: flex;
            gap: 8px;
            align-items: center;
        }

        .meta-label {
            font-weight: 600;
            color: #6c757d;
        }

        .meta-value {
            color: #2c3e50;
        }

        .code-snippet {
            background: #2c3e50;
            color: #ecf0f1;
            padding: 15px;
            border-radius: 6px;
            font-family: 'Monaco', 'Courier New', monospace;
            font-size: 0.9em;
            overflow-x: auto;
            white-space: pre-wrap;
            word-break: break-all;
            margin-top: 10px;
        }

        .summary-section {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
            gap: 20px;
            margin-bottom: 30px;
        }

        .summary-card {
            padding: 20px;
            background: white;
            border-radius: 8px;
            border: 1px solid #dee2e6;
        }

        .summary-card h3 {
            margin-bottom: 15px;
            color: #667eea;
            font-size: 1.1em;
        }

        .summary-card ul {
            list-style: none;
            padding: 0;
        }

        .summary-card li {
            padding: 8px 0;
            border-bottom: 1px solid #f0f0f0;
            display: flex;
            justify-content: space-between;
            align-items: center;
        }

        .summary-card li:last-child {
            border-bottom: none;
        }

        .rule-count {
            background: #667eea;
            color: white;
            padding: 4px 8px;
            border-radius: 4px;
            font-weight: 700;
            font-size: 0.9em;
        }

        .footer {
            padding: 20px 40px;
            background: #f8f9fa;
            border-top: 1px solid #dee2e6;
            text-align: center;
            color: #6c757d;
            font-size: 0.9em;
        }

        @media (max-width: 768px) {
            .header h1 {
                font-size: 1.8em;
            }

            .header {
                padding: 25px;
            }

            .findings-section {
                padding: 20px;
            }

            .metadata {
                padding: 20px;
            }

            .finding-header {
                flex-direction: column;
            }
        }

        .no-findings {
            text-align: center;
            padding: 60px 40px;
            color: #6c757d;
        }

        .no-findings h2 {
            font-size: 1.5em;
            margin-bottom: 10px;
            color: #2c3e50;
        }

        .confidence-badge {
            display: inline-block;
            padding: 4px 10px;
            background: #e7f3ff;
            color: #0066cc;
            border-radius: 4px;
            font-size: 0.85em;
            font-weight: 600;
        }";
    }

    private static string GetJavaScript()
    {
        return @"let allFindings = [];
        let currentFilter = 'all';

        function initializeReport(jsonData) {
            allFindings = jsonData.Findings || [];
            renderReport(jsonData);
        }

        function renderReport(data) {
            renderMetadata(data);
            renderSummary(data);
            renderFindings(data.Findings || []);
            renderFooter(data);
        }

        function renderMetadata(data) {
            const metadata = document.getElementById('metadata');
            const severity1 = (data.Findings || []).filter(f => f.Severity === 1).length;
            const severity2 = (data.Findings || []).filter(f => f.Severity === 2).length;
            const severity3 = (data.Findings || []).filter(f => f.Severity === 3).length;
            const uniqueRules = new Set((data.Findings || []).map(f => f.RuleId)).size;

            // Update filter button counts
            document.getElementById('btn-severity-1').textContent = `🔴 Block (${severity1})`;
            document.getElementById('btn-severity-2').textContent = `🟠 Warning (${severity2})`;
            document.getElementById('btn-severity-3').textContent = `🔵 Info (${severity3})`;

            metadata.innerHTML = `
                <div class=""stat-card"">
                    <div class=""stat-label"">Total Findings</div>
                    <div class=""stat-value"">${(data.Findings || []).length}</div>
                </div>
                <div class=""stat-card"">
                    <div class=""stat-label"">Block Issues</div>
                    <div class=""stat-value"" style=""color: #dc3545;"">${severity1}</div>
                </div>
                <div class=""stat-card"">
                    <div class=""stat-label"">Warnings</div>
                    <div class=""stat-value"" style=""color: #fd7e14;"">${severity2}</div>
                </div>
                <div class=""stat-card"">
                    <div class=""stat-label"">Info Items</div>
                    <div class=""stat-value"" style=""color: #0dcaf0;"">${severity3}</div>
                </div>
                <div class=""stat-card"">
                    <div class=""stat-label"">Unique Rules</div>
                    <div class=""stat-value"" style=""color: #667eea;"">${uniqueRules}</div>
                </div>
                <div class=""stat-card"">
                    <div class=""stat-label"">Rules Evaluated</div>
                    <div class=""stat-value"">${data.RulesEvaluated || 37}</div>
                </div>
            `;
        }

        function renderSummary(data) {
            const summary = document.getElementById('summary');
            const ruleMap = {};
            
            (data.Findings || []).forEach(f => {
                if (!ruleMap[f.RuleId]) {
                    ruleMap[f.RuleId] = { count: 0, name: f.RuleName };
                }
                ruleMap[f.RuleId].count++;
            });

            const rules = Object.entries(ruleMap).sort((a, b) => b[1].count - a[1].count);
            
            summary.innerHTML = `
                <div class=""summary-card"">
                    <h3>📊 Rules with Most Findings</h3>
                    <ul>
                        ${rules.slice(0, 5).map(([id, data]) => `
                            <li>
                                <span><strong>${id}</strong>: ${data.name}</span>
                                <span class=""rule-count"">${data.count}</span>
                            </li>
                        `).join('')}
                    </ul>
                </div>
                <div class=""summary-card"">
                    <h3>📈 Severity Distribution</h3>
                    <ul>
                        <li>
                            <span>🔴 Block (Critical)</span>
                            <span class=""rule-count"">${(data.Findings || []).filter(f => f.Severity === 1).length}</span>
                        </li>
                        <li>
                            <span>🟠 Warning (Medium)</span>
                            <span class=""rule-count"">${(data.Findings || []).filter(f => f.Severity === 2).length}</span>
                        </li>
                        <li>
                            <span>🔵 Info (Low)</span>
                            <span class=""rule-count"">${(data.Findings || []).filter(f => f.Severity === 3).length}</span>
                        </li>
                    </ul>
                </div>
            `;
        }

        function renderFindings(findings) {
            const container = document.getElementById('findings');
            
            if (!findings || findings.length === 0) {
                container.innerHTML = `
                    <div class=""no-findings"">
                        <h2>✨ No Findings</h2>
                        <p>This code passed all GauntletCI checks!</p>
                    </div>
                `;
                return;
            }

            const filtered = currentFilter === 'all' 
                ? findings 
                : findings.filter(f => f.Severity === currentFilter);

            if (filtered.length === 0) {
                container.innerHTML = `
                    <div class=""no-findings"">
                        <h2>No findings with this severity</h2>
                    </div>
                `;
                return;
            }

            container.innerHTML = filtered.map(finding => `
                <div class=""finding-card severity-${finding.Severity}"">
                    <div class=""finding-header"">
                        <div class=""finding-title"">
                            <span class=""rule-id"">${finding.RuleId}</span>
                            <div class=""rule-name"">${finding.RuleName}</div>
                        </div>
                        <span class=""severity-badge severity-${finding.Severity}"">
                            ${getSeverityLabel(finding.Severity)}
                        </span>
                    </div>

                    <div class=""finding-content"">
                        <div class=""finding-section"">
                            <div class=""finding-label"">📋 Summary</div>
                            <div class=""finding-text"">${escapeHtml(finding.Summary)}</div>
                        </div>

                        ${finding.Evidence ? `
                            <div class=""finding-section"">
                                <div class=""finding-label"">🔍 Evidence</div>
                                <div class=""finding-text"">${escapeHtml(finding.Evidence)}</div>
                            </div>
                        ` : ''}

                        ${finding.WhyItMatters ? `
                            <div class=""finding-section"">
                                <div class=""finding-label"">⚠️ Why It Matters</div>
                                <div class=""finding-text"">${escapeHtml(finding.WhyItMatters)}</div>
                            </div>
                        ` : ''}

                        ${finding.SuggestedAction ? `
                            <div class=""finding-section"">
                                <div class=""finding-label"">✅ Suggested Action</div>
                                <div class=""finding-text"">${escapeHtml(finding.SuggestedAction)}</div>
                            </div>
                        ` : ''}

                        ${finding.CodeSnippet ? `
                            <div class=""finding-section"">
                                <div class=""finding-label"">💻 Code Snippet</div>
                                <div class=""code-snippet"">${escapeHtml(finding.CodeSnippet)}</div>
                            </div>
                        ` : ''}
                    </div>

                    <div class=""finding-meta"">
                        <div class=""meta-item"">
                            <span class=""meta-label"">Confidence:</span>
                            <span class=""confidence-badge"">${(finding.Confidence * 100).toFixed(0)}%</span>
                        </div>
                        ${finding.FilePath ? `
                            <div class=""meta-item"">
                                <span class=""meta-label"">File:</span>
                                <span class=""meta-value"">${escapeHtml(finding.FilePath)}</span>
                            </div>
                        ` : ''}
                        ${finding.Line ? `
                            <div class=""meta-item"">
                                <span class=""meta-label"">Line:</span>
                                <span class=""meta-value"">${finding.Line}</span>
                            </div>
                        ` : ''}
                    </div>
                </div>
            `).join('');
        }

        function getSeverityLabel(severity) {
            switch(severity) {
                case 1: return '🔴 Block';
                case 2: return '🟠 Warning';
                case 3: return '🔵 Info';
                default: return 'Unknown';
            }
        }

        function filterBySeverity(severity) {
            currentFilter = severity;
            renderFindings(allFindings);
            
            document.querySelectorAll('.filter-btn').forEach(btn => {
                btn.classList.remove('active');
            });
            event.target.classList.add('active');
        }

        function renderFooter(data) {
            const footer = document.getElementById('footer');
            const now = new Date().toLocaleString();
            footer.innerHTML = `
                <p><a href=""https://github.com/gaunlet-ai/gauntletci"" target=""_blank"" style=""color: #667eea; text-decoration: none; font-weight: 600;"">GauntletCI Analysis Report</a> | Generated: ${now}</p>
                <p style=""margin-top: 10px; font-size: 0.85em;"">Rules Evaluated: ${data.RulesEvaluated || 37} | Total Findings: ${(data.Findings || []).length}</p>
            `;
        }

        function escapeHtml(text) {
            if (!text) return '';
            const div = document.createElement('div');
            div.textContent = text;
            return div.innerHTML;
        }";
    }
}
