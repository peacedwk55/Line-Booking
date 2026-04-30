'use client'
// app/my-bookings/page.tsx  —  User's booking history
import { useState, useEffect } from 'react'
import { useLiff } from '@/hooks/useLiff'
import { getUserBookings, cancelBooking, Booking } from '@/lib/api'
import { format } from 'date-fns'
import { th } from 'date-fns/locale'
import Link from 'next/link'

const STATUS_LABELS: Record<string, { label: string; color: string }> = {
  Confirmed:  { label: 'ยืนยันแล้ว',  color: 'bg-green-100 text-green-700' },
  Pending:    { label: 'รอยืนยัน',    color: 'bg-yellow-100 text-yellow-700' },
  Cancelled:  { label: 'ยกเลิกแล้ว', color: 'bg-red-100 text-red-600' },
  Completed:  { label: 'เสร็จสิ้น',   color: 'bg-gray-100 text-gray-600' },
}

export default function MyBookingsPage() {
  const { profile, ready } = useLiff()
  const [bookings, setBookings] = useState<Booking[]>([])
  const [loading,  setLoading]  = useState(true)

  const load = async () => {
    if (!profile) return
    setLoading(true)
    try {
      const data = await getUserBookings(profile.userId)
      setBookings(data)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => { if (ready) load() }, [ready, profile])

  const handleCancel = async (id: string) => {
    if (!profile) return
    if (!confirm('ยืนยันการยกเลิกการจอง?')) return
    try {
      await cancelBooking(id, profile.userId)
      await load()
    } catch (e: any) {
      alert(e?.response?.data?.message ?? 'ไม่สามารถยกเลิกได้')
    }
  }

  const upcoming = bookings.filter(b => b.status !== 'Cancelled' && b.status !== 'Completed')
  const past     = bookings.filter(b => b.status === 'Cancelled' || b.status === 'Completed')

  if (!ready) return (
    <div className="min-h-screen flex items-center justify-center bg-amber-50">
      <div className="w-10 h-10 border-4 border-amber-400 border-t-transparent rounded-full animate-spin"/>
    </div>
  )

  return (
    <div className="min-h-screen bg-amber-50">
      <div className="bg-gradient-to-r from-amber-600 to-amber-500 text-white px-4 py-5">
        <div className="flex items-center justify-between">
          <div>
            <h1 className="font-bold text-lg">💎 การจองของฉัน</h1>
            <p className="text-amber-100 text-sm">Diamond Massage</p>
          </div>
          <Link href="/" className="text-white/80 text-sm border border-white/30 px-3 py-1.5 rounded-xl hover:bg-white/10">
            + จองใหม่
          </Link>
        </div>
      </div>

      <div className="p-4 max-w-lg mx-auto">
        {loading ? (
          <div className="text-center py-12 text-amber-600">กำลังโหลด...</div>
        ) : bookings.length === 0 ? (
          <div className="text-center py-16">
            <p className="text-4xl mb-3">📭</p>
            <p className="text-gray-600 mb-4">ยังไม่มีการจอง</p>
            <Link href="/" className="bg-amber-500 text-white px-6 py-3 rounded-2xl font-medium">
              จองนัดหมาย
            </Link>
          </div>
        ) : (
          <>
            {upcoming.length > 0 && (
              <>
                <h2 className="font-bold text-gray-700 mb-3">นัดที่กำลังจะมาถึง</h2>
                <div className="space-y-3 mb-6">
                  {upcoming.map(b => <BookingCard key={b.id} booking={b} onCancel={handleCancel}/>)}
                </div>
              </>
            )}
            {past.length > 0 && (
              <>
                <h2 className="font-bold text-gray-500 mb-3 text-sm">ประวัติ</h2>
                <div className="space-y-2">
                  {past.map(b => <BookingCard key={b.id} booking={b} past/>)}
                </div>
              </>
            )}
          </>
        )}
      </div>
    </div>
  )
}

function BookingCard({ booking, onCancel, past = false }: {
  booking: Booking
  onCancel?: (id: string) => void
  past?: boolean
}) {
  const { label, color } = STATUS_LABELS[booking.status] ?? { label: booking.status, color: 'bg-gray-100 text-gray-600' }
  const dateLabel = format(new Date(booking.date), 'EEEE d MMMM yyyy', { locale: th })
  const canCancel = !past && booking.status === 'Confirmed'

  return (
    <div className={`bg-white rounded-2xl p-4 shadow-sm border ${past ? 'border-gray-100 opacity-70' : 'border-amber-100'}`}>
      <div className="flex justify-between items-start mb-2">
        <div>
          <p className="font-semibold text-gray-800">{booking.service}</p>
          <p className="text-xs text-gray-400 mt-0.5">#{booking.id.slice(0,8)}</p>
        </div>
        <span className={`text-xs px-2 py-1 rounded-full font-medium ${color}`}>{label}</span>
      </div>
      <div className="text-sm text-gray-600 space-y-1">
        <p>📅 {dateLabel}</p>
        <p>⏰ {booking.startTime} – {booking.endTime} น.</p>
      </div>
      {canCancel && onCancel && (
        <button
          onClick={() => onCancel(booking.id)}
          className="mt-3 w-full text-sm text-red-500 border border-red-200 py-2 rounded-xl hover:bg-red-50 transition-all"
        >
          ยกเลิกการจอง
        </button>
      )}
    </div>
  )
}
