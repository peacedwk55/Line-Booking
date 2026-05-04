'use client'
// app/page.tsx  —  LIFF Booking App (Multi-step)
import { useState, useEffect } from 'react'
import { useLiff } from '@/hooks/useLiff'
import {
  getServices, getSlots, createBooking,
  Service, Slot
} from '@/lib/api'
import { format, addDays } from 'date-fns'
import { th } from 'date-fns/locale'

type Step = 'service' | 'duration' | 'date' | 'time' | 'confirm' | 'done'

const DURATIONS = [
  { minutes: 60,  label: '1 ชั่วโมง' },
  { minutes: 90,  label: '1.5 ชั่วโมง' },
  { minutes: 120, label: '2 ชั่วโมง' },
]

export default function BookingPage() {
  const { profile, ready, error } = useLiff()

  const [step,            setStep]     = useState<Step>('service')
  const [services,        setServices] = useState<Service[]>([])
  const [selectedService, setService]  = useState<Service | null>(null)
  const [selectedDuration, setDuration] = useState<number>(60)
  const [selectedDate,    setDate]     = useState<string>('')
  const [slots,           setSlots]    = useState<Slot[]>([])
  const [selectedSlot,    setSlot]     = useState<Slot | null>(null)
  const [note,            setNote]     = useState('')
  const [loading,         setLoading]  = useState(false)
  const [bookingId,       setBookingId] = useState<string>('')
  const [apiError,        setApiError]  = useState<string>('')

  const minDate = format(new Date(), 'yyyy-MM-dd')
  const maxDate = format(addDays(new Date(), 60), 'yyyy-MM-dd')

  const totalPrice = selectedService
    ? Math.round(selectedService.pricePerHour * selectedDuration / 60)
    : 0

  useEffect(() => {
    if (ready) getServices().then(setServices).catch(e => setApiError(e?.message ?? 'API error'))
  }, [ready])

  useEffect(() => {
    if (selectedDate) {
      setSlot(null)
      setLoading(true)
      getSlots(selectedDate, selectedDuration)
        .then(data => {
          const today = format(new Date(), 'yyyy-MM-dd')
          if (selectedDate === today) {
            const now = new Date()
            const hhmm = `${String(now.getHours()).padStart(2,'0')}:${String(now.getMinutes()).padStart(2,'0')}`
            setSlots(data.filter(s => s.start >= hhmm))
          } else {
            setSlots(data)
          }
        })
        .catch(() => setSlots([]))
        .finally(() => setLoading(false))
    }
  }, [selectedDate, selectedDuration])

  const handleConfirm = async () => {
    if (!profile || !selectedService || !selectedSlot) return
    setLoading(true)
    try {
      const result = await createBooking({
        lineUserId:      profile.userId,
        displayName:     profile.displayName,
        pictureUrl:      profile.pictureUrl,
        serviceId:       selectedService.id,
        date:            selectedDate,
        startTime:       selectedSlot.start,
        endTime:         selectedSlot.end,
        durationMinutes: selectedDuration,
        note
      })
      setBookingId(result.bookingId)
      setStep('done')
    } catch (e: any) {
      alert(e?.response?.data?.message ?? 'เกิดข้อผิดพลาด กรุณาลองใหม่')
    } finally {
      setLoading(false)
    }
  }

  // ── Loading state ────────────────────────────────────────────────
  if (!ready && !error) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-amber-50">
        <div className="text-center">
          <div className="w-12 h-12 border-4 border-amber-400 border-t-transparent rounded-full animate-spin mx-auto mb-4"/>
          <p className="text-amber-700 font-medium">กำลังโหลด...</p>
        </div>
      </div>
    )
  }

  if (error) {
    return (
      <div className="min-h-screen flex items-center justify-center bg-red-50 p-6">
        <div className="text-center text-red-600">
          <p className="text-xl mb-2">⚠️</p>
          <p>เกิดข้อผิดพลาด กรุณาเปิดใหม่ผ่าน LINE</p>
        </div>
      </div>
    )
  }

  const STEPS: Step[] = ['service', 'duration', 'date', 'time', 'confirm']

  return (
    <div className="min-h-screen bg-amber-50">
      {/* Header */}
      <div className="bg-gradient-to-r from-amber-600 to-amber-500 text-white px-4 py-5">
        <div className="flex items-center gap-3">
          {profile?.pictureUrl && (
            <img src={profile.pictureUrl} className="w-9 h-9 rounded-full border-2 border-white/50" alt="avatar"/>
          )}
          <div>
            <h1 className="font-bold text-lg leading-tight">💎 Diamond Massage</h1>
            <p className="text-amber-100 text-sm">สวัสดี {profile?.displayName} ✨</p>
          </div>
        </div>

        {/* Step indicator */}
        {step !== 'done' && (
          <div className="flex gap-1.5 mt-4">
            {STEPS.map((s, i) => (
              <div key={s} className={`h-1.5 flex-1 rounded-full transition-all ${
                STEPS.indexOf(step) >= i ? 'bg-white' : 'bg-white/30'
              }`}/>
            ))}
          </div>
        )}
      </div>

      <div className="p-4 max-w-lg mx-auto">

        {/* ── STEP 1: Select Service ── */}
        {step === 'service' && (
          <div>
            <h2 className="font-bold text-gray-800 text-lg mt-2 mb-4">เลือกบริการ</h2>
            {apiError && <p className="text-red-500 text-sm mb-3 bg-red-50 p-3 rounded-xl">❌ {apiError}</p>}
            <div className="space-y-3">
              {services.map(svc => (
                <button
                  key={svc.id}
                  onClick={() => { setService(svc); setStep('duration') }}
                  className="w-full bg-white rounded-2xl p-4 text-left shadow-sm border border-amber-100 hover:border-amber-400 hover:shadow-md active:scale-[0.98] transition-all"
                >
                  <div className="flex justify-between items-start">
                    <div className="flex-1">
                      <p className="font-semibold text-gray-800">{svc.name}</p>
                      <p className="text-sm text-gray-500 mt-0.5">{svc.description}</p>
                    </div>
                    <div className="ml-3 text-right shrink-0">
                      <span className="text-amber-600 font-bold">{svc.pricePerHour?.toLocaleString()}</span>
                      <span className="text-gray-400 text-xs"> ฿/ชม</span>
                    </div>
                  </div>
                </button>
              ))}
            </div>
          </div>
        )}

        {/* ── STEP 2: Select Duration ── */}
        {step === 'duration' && (
          <div>
            <button onClick={() => setStep('service')} className="text-amber-600 text-sm mb-4 flex items-center gap-1">
              ← กลับ
            </button>
            <h2 className="font-bold text-gray-800 text-lg mb-1">เลือกระยะเวลา</h2>
            <p className="text-sm text-gray-500 mb-4">{selectedService?.name}</p>
            <div className="space-y-3">
              {DURATIONS.map(d => {
                const price = Math.round(selectedService!.pricePerHour * d.minutes / 60)
                return (
                  <button
                    key={d.minutes}
                    onClick={() => { setDuration(d.minutes); setStep('date') }}
                    className={`w-full rounded-2xl p-4 text-left transition-all border ${
                      selectedDuration === d.minutes
                        ? 'bg-amber-500 border-amber-500 text-white shadow-md'
                        : 'bg-white border-amber-100 hover:border-amber-400 text-gray-700 active:scale-[0.98]'
                    }`}
                  >
                    <div className="flex justify-between items-center">
                      <span className="font-semibold">{d.label}</span>
                      <span className="font-bold">{price.toLocaleString()} ฿</span>
                    </div>
                  </button>
                )
              })}
            </div>
          </div>
        )}

        {/* ── STEP 3: Select Date ── */}
        {step === 'date' && (
          <div>
            <button onClick={() => setStep('duration')} className="text-amber-600 text-sm mb-4 flex items-center gap-1">
              ← กลับ
            </button>
            <h2 className="font-bold text-gray-800 text-lg mb-1">เลือกวันที่</h2>
            <p className="text-sm text-gray-500 mb-1">{selectedService?.name} · {DURATIONS.find(d => d.minutes === selectedDuration)?.label}</p>
            <p className="text-xs text-gray-400 mb-3">เปิดให้จองวันจันทร์ – เสาร์</p>
            <input
              type="date"
              min={minDate}
              max={maxDate}
              value={selectedDate}
              onChange={e => { if (e.target.value >= minDate) setDate(e.target.value) }}
              className="w-full border-2 border-amber-200 rounded-2xl p-4 text-lg text-gray-700 focus:outline-none focus:border-amber-400 bg-white"
            />
            {selectedDate && new Date(selectedDate).getDay() === 0 && (
              <p className="text-red-500 text-sm mt-2">วันอาทิตย์ไม่เปิดให้บริการ กรุณาเลือกวันอื่น</p>
            )}
            <button
              onClick={() => setStep('time')}
              disabled={!selectedDate || new Date(selectedDate).getDay() === 0}
              className="w-full mt-4 bg-amber-500 hover:bg-amber-600 active:scale-[0.98] text-white font-bold py-4 rounded-2xl shadow-md transition-all disabled:opacity-40 disabled:cursor-not-allowed"
            >
              ถัดไป →
            </button>
          </div>
        )}

        {/* ── STEP 4: Select Time ── */}
        {step === 'time' && (
          <div>
            <button onClick={() => setStep('date')} className="text-amber-600 text-sm mb-4 flex items-center gap-1">
              ← กลับ
            </button>
            <h2 className="font-bold text-gray-800 text-lg mb-1">เลือกเวลาเริ่ม</h2>
            <p className="text-sm text-gray-500 mb-4">
              {selectedDate && format(new Date(selectedDate), 'EEEE d MMMM yyyy', { locale: th })}
              {' · '}{DURATIONS.find(d => d.minutes === selectedDuration)?.label}
            </p>
            {loading ? (
              <div className="text-center py-8 text-amber-600">กำลังโหลดช่วงเวลา...</div>
            ) : slots.length === 0 ? (
              <div className="text-center py-10 text-gray-400">
                <p className="text-3xl mb-2">😔</p>
                <p className="font-medium">ไม่มีช่วงเวลาว่างสำหรับวันนี้</p>
                <p className="text-sm mt-1">กรุณาเลือกวันอื่น</p>
              </div>
            ) : (
              <div className="grid grid-cols-2 gap-3">
                {slots.map(slot => (
                  <button
                    key={slot.start}
                    onClick={() => { setSlot(slot); setStep('confirm') }}
                    disabled={!slot.available}
                    className={`rounded-xl p-3.5 text-center transition-all ${
                      !slot.available
                        ? 'bg-gray-100 text-gray-400 cursor-not-allowed'
                        : selectedSlot?.start === slot.start
                          ? 'bg-amber-500 text-white shadow-md'
                          : 'bg-white border border-amber-100 hover:border-amber-400 text-gray-700 active:scale-[0.97]'
                    }`}
                  >
                    <p className="font-bold">{slot.start} – {slot.end}</p>
                    <p className="text-xs mt-0.5 opacity-70">
                      {slot.available ? `ว่าง ${slot.remaining} คิว` : 'เต็ม'}
                    </p>
                  </button>
                ))}
              </div>
            )}
          </div>
        )}

        {/* ── STEP 5: Confirm ── */}
        {step === 'confirm' && (
          <div>
            <button onClick={() => setStep('time')} className="text-amber-600 text-sm mb-4 flex items-center gap-1">
              ← กลับ
            </button>
            <h2 className="font-bold text-gray-800 text-lg mb-4">ยืนยันการจอง</h2>

            <div className="bg-white rounded-2xl shadow-sm border border-amber-100 overflow-hidden mb-4">
              <div className="bg-amber-50 px-4 py-3 border-b border-amber-100">
                <p className="font-semibold text-amber-800">💎 Diamond Massage</p>
              </div>
              <div className="p-4 space-y-3">
                {[
                  ['💆 บริการ',     selectedService?.name],
                  ['⏱ ระยะเวลา',  DURATIONS.find(d => d.minutes === selectedDuration)?.label],
                  ['📅 วันที่',     selectedDate && format(new Date(selectedDate), 'd MMMM yyyy', { locale: th })],
                  ['⏰ เวลา',       `${selectedSlot?.start} – ${selectedSlot?.end} น.`],
                  ['💰 ราคา',      `${totalPrice.toLocaleString()} ฿`],
                ].map(([label, value]) => (
                  <div key={label} className="flex justify-between text-sm">
                    <span className="text-gray-500">{label}</span>
                    <span className="font-medium text-gray-800">{value}</span>
                  </div>
                ))}
              </div>
            </div>

            <div className="mb-4">
              <label className="text-sm text-gray-600 mb-1.5 block">หมายเหตุ (ไม่บังคับ)</label>
              <textarea
                value={note}
                onChange={e => setNote(e.target.value)}
                placeholder="เช่น มีอาการปวดหลัง, แพ้น้ำหอม..."
                rows={3}
                className="w-full border border-gray-200 rounded-xl p-3 text-sm resize-none focus:outline-none focus:ring-2 focus:ring-amber-300"
              />
            </div>

            <button
              onClick={handleConfirm}
              disabled={loading}
              className="w-full bg-amber-500 hover:bg-amber-600 active:scale-[0.98] text-white font-bold py-4 rounded-2xl shadow-md transition-all disabled:opacity-60 disabled:cursor-not-allowed text-base"
            >
              {loading ? '⏳ กำลังจอง...' : '✅ ยืนยันการจอง'}
            </button>
          </div>
        )}

        {/* ── STEP 6: Done ── */}
        {step === 'done' && (
          <div className="text-center py-8">
            <div className="text-6xl mb-4">🎉</div>
            <h2 className="font-bold text-2xl text-gray-800 mb-2">จองสำเร็จ!</h2>
            <p className="text-gray-500 mb-2">Diamond Massage รอต้อนรับคุณค่ะ</p>
            <p className="text-xs text-gray-400 mb-6">เราจะส่งการแจ้งเตือนก่อนนัด 1 วัน</p>

            <div className="bg-amber-50 rounded-2xl p-4 text-left mb-6 border border-amber-100">
              <p className="text-sm text-gray-600">📅 {selectedDate && format(new Date(selectedDate), 'd MMMM yyyy', { locale: th })}</p>
              <p className="text-sm text-gray-600">⏰ {selectedSlot?.start} – {selectedSlot?.end} น.</p>
              <p className="text-sm text-gray-600">💆 {selectedService?.name} · {DURATIONS.find(d => d.minutes === selectedDuration)?.label}</p>
              <p className="text-sm font-semibold text-amber-700">💰 {totalPrice.toLocaleString()} ฿</p>
              <p className="text-xs text-gray-400 mt-2">ID: {bookingId.slice(0, 8)}</p>
            </div>

            <button
              onClick={() => {
                setStep('service'); setService(null); setDuration(60)
                setDate(''); setSlot(null); setNote(''); setBookingId('')
              }}
              className="w-full border border-amber-400 text-amber-600 font-semibold py-3.5 rounded-2xl hover:bg-amber-50 transition-all"
            >
              จองอีกครั้ง
            </button>
          </div>
        )}
      </div>
    </div>
  )
}
