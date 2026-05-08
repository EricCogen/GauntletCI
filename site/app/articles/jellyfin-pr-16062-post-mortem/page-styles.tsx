'use client';

export function PageStyles() {
  return (
    <style jsx>{`
      .article-container {
        max-width: 900px;
        margin: 0 auto;
        padding: 2rem;
        line-height: 1.7;
      }

      .article-header {
        margin-bottom: 2rem;
      }

      .article-header h1 {
        font-size: 2.5rem;
        margin-bottom: 0.5rem;
        font-weight: 700;
        color: #1a1a1a;
      }

      .article-meta {
        display: flex;
        gap: 1rem;
        color: #666;
        font-size: 0.95rem;
      }

      .published-date,
      .read-time {
        font-style: italic;
      }

      .lead {
        font-size: 1.2rem;
        font-weight: 500;
        margin: 1.5rem 0;
        color: #333;
      }

      h2 {
        margin-top: 2rem;
        margin-bottom: 1rem;
        font-size: 1.8rem;
        border-bottom: 2px solid #eee;
        padding-bottom: 0.5rem;
        font-weight: 700;
      }

      hr {
        margin: 2rem 0;
        border: none;
        border-top: 1px solid #eee;
      }

      .findings-grid {
        display: grid;
        grid-template-columns: repeat(auto-fit, minmax(300px, 1fr));
        gap: 1.5rem;
        margin: 2rem 0;
      }

      .finding-card {
        border: 1px solid #e0e0e0;
        border-radius: 8px;
        padding: 1.5rem;
        background: #fafafa;
      }

      .finding-header {
        display: flex;
        justify-content: space-between;
        align-items: center;
        margin-bottom: 0.5rem;
      }

      .rule-id {
        font-weight: bold;
        color: #667eea;
        font-size: 0.9rem;
        font-family: "Courier New", monospace;
      }

      .severity {
        padding: 0.25rem 0.75rem;
        border-radius: 4px;
        font-size: 0.85rem;
        font-weight: 600;
      }

      .severity.block {
        background: #dc3545;
        color: white;
      }

      .severity.warn {
        background: #fd7e14;
        color: white;
      }

      .finding-card h3 {
        margin: 0.5rem 0;
        font-size: 1.1rem;
        font-weight: 600;
      }

      .finding-count {
        display: inline-block;
        background: #667eea;
        color: white;
        padding: 0.25rem 0.75rem;
        border-radius: 4px;
        font-size: 0.9rem;
        margin-bottom: 0.75rem;
        font-weight: 600;
      }

      .finding-description {
        margin: 0.75rem 0;
        font-size: 0.95rem;
      }

      .finding-impact {
        margin: 0.75rem 0;
        font-size: 0.9rem;
        color: #555;
        background: #f5f5f5;
        padding: 0.75rem;
        border-left: 3px solid #667eea;
        border-radius: 4px;
      }

      pre {
        background: #f4f4f4;
        padding: 1rem;
        border-radius: 6px;
        overflow-x: auto;
        border-left: 3px solid #667eea;
        margin: 1rem 0;
      }

      code {
        font-family: "Monaco", "Courier New", monospace;
        font-size: 0.9rem;
      }

      .links {
        list-style: none;
        padding: 0;
        margin: 1.5rem 0;
      }

      .links li {
        margin: 0.75rem 0;
      }

      .links a {
        color: #667eea;
        text-decoration: none;
        font-weight: 500;
        transition: color 0.2s;
      }

      .links a:hover {
        text-decoration: underline;
        color: #5568d3;
      }

      p {
        margin: 1rem 0;
      }

      strong {
        font-weight: 600;
      }

      em {
        font-style: italic;
      }

      @media (max-width: 768px) {
        .article-container {
          padding: 1rem;
        }

        .article-header h1 {
          font-size: 1.8rem;
        }

        .findings-grid {
          grid-template-columns: 1fr;
        }

        .article-meta {
          flex-direction: column;
          gap: 0.25rem;
        }
      }
    `}</style>
  );
}
