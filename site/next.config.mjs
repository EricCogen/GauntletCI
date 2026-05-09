/** @type {import('next').NextConfig} */
const nextConfig = {
  output: 'export',
  experimental: {
    // Disable Turbopack to fix static export routing issues
    // Turbopack generates __next.* files instead of index.html
    turbopack: false,
  },
  typescript: {
    ignoreBuildErrors: true,
  },
  images: {
    unoptimized: true,
  },
}

export default nextConfig
