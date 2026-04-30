// app/layout.tsx
import type { Metadata } from 'next'
import './globals.css'

export const metadata: Metadata = {
  title: 'Diamond Massage - จองนัดหมาย',
  description: 'ระบบจองนัดหมายนวด Diamond Massage',
}

export default function RootLayout({ children }: { children: React.ReactNode }) {
  return (
    <html lang="th">
      <head>
        <meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1"/>
      </head>
      <body className="font-sans antialiased">{children}</body>
    </html>
  )
}
