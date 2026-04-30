// lib/api.ts
import axios from 'axios'

const BASE  = process.env.NEXT_PUBLIC_API_URL!
const SLUG  = process.env.NEXT_PUBLIC_TENANT_SLUG!

const api = axios.create({ baseURL: `${BASE}/api/${SLUG}` })

// ── Services ──────────────────────────────────────────────────────
export async function getServices() {
  const { data } = await api.get('/services')
  return data as Service[]
}

// ── Slots ─────────────────────────────────────────────────────────
export async function getSlots(date: string) {
  const { data } = await api.get(`/bookings/slots?date=${date}`)
  return data as Slot[]
}

// ── Bookings ──────────────────────────────────────────────────────
export async function createBooking(payload: CreateBookingPayload) {
  const { data } = await api.post('/bookings', payload)
  return data
}

export async function getUserBookings(lineUserId: string) {
  const { data } = await api.get(`/bookings?lineUserId=${lineUserId}`)
  return data as Booking[]
}

export async function cancelBooking(id: string, lineUserId: string) {
  const { data } = await api.delete(`/bookings/${id}?lineUserId=${lineUserId}`)
  return data
}

// ── Types ─────────────────────────────────────────────────────────
export interface Service {
  id:              string
  name:            string
  description:     string
  durationMinutes: number
  price:           number
}

export interface Slot {
  start:     string
  end:       string
  available: boolean
  remaining: number
}

export interface CreateBookingPayload {
  lineUserId: string
  serviceId:  string
  date:       string
  startTime:  string
  endTime:    string
  note?:      string
}

export interface Booking {
  id:        string
  date:      string
  startTime: string
  endTime:   string
  service:   string
  status:    string
  createdAt: string
}
