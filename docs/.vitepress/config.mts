import { defineConfig } from 'vitepress';

const jsonLd = JSON.stringify({
  '@context': 'https://schema.org',
  '@graph': [
    {
      '@type': 'SoftwareSourceCode',
      'name': 'KeysetEngine',
      'description': 'High-performance, database-agnostic keyset pagination and universal search library for .NET 8, 9, and 10 with Entity Framework Core.',
      'programmingLanguage': 'C#',
      'runtimePlatform': ['.NET 8.0', '.NET 9.0', '.NET 10.0'],
      'codeRepository': 'https://github.com/aebibtech/keysetengine',
      'license': 'https://opensource.org/licenses/MIT',
      'author': {
        '@type': 'Person',
        'name': 'Paul Abib Camano',
        'url': 'https://github.com/aebibtech'
      }
    },
    {
      '@type': 'WebSite',
      'name': 'KeysetEngine Documentation',
      'url': 'https://keysetengine.aebibtech.com',
      'description': 'Complete guides, architectural specifications, and API documentation for KeysetEngine .NET pagination library.'
    }
  ]
});

export default defineConfig({
  title: 'KeysetEngine',
  description: 'High-performance, database-agnostic keyset pagination and search engine for .NET and EF Core',
  base: '/',
  lang: 'en-US',
  lastUpdated: true,
  cleanUrls: true,

  sitemap: {
    hostname: 'https://keysetengine.aebibtech.com'
  },

  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }],
    ['link', { rel: 'canonical', href: 'https://keysetengine.aebibtech.com' }],
    ['meta', { name: 'theme-color', content: '#2563eb' }],
    ['meta', { name: 'author', content: 'Paul Abib Camano' }],
    ['meta', { name: 'keywords', content: '.NET, Keyset Pagination, Cursor Pagination, EF Core, Entity Framework Core, PostgreSQL, SQL Server, SQLite, High Performance, C#, ASP.NET Core' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:site_name', content: 'KeysetEngine' }],
    ['meta', { property: 'og:title', content: 'KeysetEngine — .NET Keyset Pagination & Search' }],
    ['meta', { property: 'og:description', content: 'High-Performance Keyset Pagination & Search for .NET and EF Core. Avoid SQL OFFSET degradation with O(1) indexed seeks.' }],
    ['meta', { property: 'og:url', content: 'https://keysetengine.aebibtech.com' }],
    ['meta', { name: 'twitter:card', content: 'summary_large_image' }],
    ['meta', { name: 'twitter:title', content: 'KeysetEngine — .NET Keyset Pagination & Search' }],
    ['meta', { name: 'twitter:description', content: 'High-Performance Keyset Pagination & Search for .NET and EF Core.' }],
    ['script', { type: 'application/ld+json' }, jsonLd]
  ],

  themeConfig: {
    siteTitle: 'KeysetEngine',
    logo: '/logo.svg',

    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'FAQ', link: '/guide/faq' },
      { text: 'Databases', link: '/databases/postgresql' },
      { text: 'Security', link: '/security/data-protection' },
      { text: 'API Reference', link: '/api/core' },
      { text: 'llms.txt', link: '/llms.txt', target: '_blank' },
      {
        text: 'NuGet',
        items: [
          { text: 'KeysetEngine.Core', link: 'https://www.nuget.org/packages/KeysetEngine.Core' },
          { text: 'KeysetEngine.DataProtection', link: 'https://www.nuget.org/packages/KeysetEngine.DataProtection' },
          { text: 'KeysetEngine.PostgreSql', link: 'https://www.nuget.org/packages/KeysetEngine.PostgreSql' },
          { text: 'KeysetEngine.SqlServer', link: 'https://www.nuget.org/packages/KeysetEngine.SqlServer' }
        ]
      }
    ],

    sidebar: [
      {
        text: 'Getting Started',
        items: [
          { text: 'Overview & Installation', link: '/guide/getting-started' },
          { text: 'Why Keyset Pagination?', link: '/guide/why-keyset' },
          { text: 'Frequently Asked Questions', link: '/guide/faq' }
        ]
      },
      {
        text: 'Core Architecture',
        items: [
          { text: 'Pipeline & Expressions', link: '/guide/architecture' },
          { text: 'Bidirectional Traversal', link: '/guide/bidirectional' },
          { text: 'Dynamic Search & Stale Cursors', link: '/guide/search' },
          { text: 'Index Design & Optimization', link: '/guide/indexing' }
        ]
      },
      {
        text: 'Security & Tokens',
        items: [
          { text: 'Cryptographic Protection', link: '/security/data-protection' }
        ]
      },
      {
        text: 'Database Adapters',
        items: [
          { text: 'PostgreSQL Strategy', link: '/databases/postgresql' },
          { text: 'SQL Server Strategy', link: '/databases/sqlserver' },
          { text: 'SQLite & In-Memory', link: '/databases/sqlite' }
        ]
      },
      {
        text: 'API Reference',
        items: [
          { text: 'KeysetEngine.Core', link: '/api/core' },
          { text: 'KeysetEngine.DataProtection', link: '/api/data-protection' },
          { text: 'KeysetEngine.PostgreSql', link: '/api/postgresql' },
          { text: 'KeysetEngine.SqlServer', link: '/api/sqlserver' }
        ]
      }
    ],

    search: {
      provider: 'local'
    },

    socialLinks: [
      { icon: 'github', link: 'https://github.com/aebibtech/keysetengine' }
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 Paul Abib Camano (Aebibtech)'
    }
  }
});
