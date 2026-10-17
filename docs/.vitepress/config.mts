import { defineConfig } from 'vitepress';

export default defineConfig({
  title: 'KeysetEngine',
  description: 'High-performance, database-agnostic keyset pagination and search engine for .NET and EF Core',
  base: '/',
  lang: 'en-US',
  lastUpdated: true,
  cleanUrls: true,

  head: [
    ['link', { rel: 'icon', type: 'image/svg+xml', href: '/logo.svg' }],
    ['meta', { name: 'theme-color', content: '#3eaf7c' }],
    ['meta', { property: 'og:type', content: 'website' }],
    ['meta', { property: 'og:title', content: 'KeysetEngine — .NET Keyset Pagination' }],
    ['meta', { property: 'og:description', content: 'High-Performance Keyset Pagination & Search for .NET and EF Core' }]
  ],

  themeConfig: {
    siteTitle: 'KeysetEngine',
    logo: '/logo.svg',

    nav: [
      { text: 'Guide', link: '/guide/getting-started' },
      { text: 'Databases', link: '/databases/postgresql' },
      { text: 'Security', link: '/security/data-protection' },
      { text: 'API Reference', link: '/api/core' },
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
          { text: 'Why Keyset Pagination?', link: '/guide/why-keyset' }
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
      { icon: 'github', link: 'https://github.com/stewardaxis/keysetengine' }
    ],

    footer: {
      message: 'Released under the MIT License.',
      copyright: 'Copyright © 2026 Paul Abib Camano (Aebibtech)'
    }
  }
});
