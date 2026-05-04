// __tests__/BookingPage.test.tsx  —  Component tests for booking flow
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import BookingPage from '@/app/page'
import * as api from '@/lib/api'
import * as liff from '@/hooks/useLiff'

// ── Mock LIFF ─────────────────────────────────────────────────────
jest.mock('@/hooks/useLiff', () => ({
  useLiff: jest.fn(),
}))

// ── Mock API ──────────────────────────────────────────────────────
jest.mock('@/lib/api', () => ({
  getServices:    jest.fn(),
  getSlots:       jest.fn(),
  createBooking:  jest.fn(),
}))

const mockProfile = {
  userId:      'U_TEST_123',
  displayName: 'คุณทดสอบ',
  pictureUrl:  undefined,
}

const mockServices: api.Service[] = [
  { id: 'svc-1', name: 'นวดแผนไทย', description: 'ผ่อนคลาย', pricePerHour: 350 },
  { id: 'svc-2', name: 'นวดน้ำมัน',  description: 'อโรมา',    pricePerHour: 450 },
]

const mockSlots: api.Slot[] = [
  { start: '10:00', end: '11:30', available: true,  remaining: 2 },
  { start: '14:00', end: '15:30', available: false, remaining: 0 },
]

function setupMocks() {
  ;(liff.useLiff as jest.Mock).mockReturnValue({
    profile: mockProfile,
    ready:   true,
    error:   null,
  })
  ;(api.getServices as jest.Mock).mockResolvedValue(mockServices)
  ;(api.getSlots    as jest.Mock).mockResolvedValue(mockSlots)
  ;(api.createBooking as jest.Mock).mockResolvedValue({
    bookingId: 'bk-test-uuid',
    message:   'จองสำเร็จ!',
  })
}

// ── Tests ─────────────────────────────────────────────────────────

describe('BookingPage', () => {

  beforeEach(() => {
    jest.clearAllMocks()
    setupMocks()
  })

  it('shows loading spinner before LIFF ready', () => {
    ;(liff.useLiff as jest.Mock).mockReturnValue({ profile: null, ready: false, error: null })
    render(<BookingPage />)
    expect(screen.getByText('กำลังโหลด...')).toBeInTheDocument()
  })

  it('shows error message when LIFF fails', () => {
    ;(liff.useLiff as jest.Mock).mockReturnValue({ profile: null, ready: false, error: 'fail' })
    render(<BookingPage />)
    expect(screen.getByText(/เกิดข้อผิดพลาด/)).toBeInTheDocument()
  })

  it('shows greeting with display name', async () => {
    render(<BookingPage />)
    await waitFor(() => {
      expect(screen.getByText(/สวัสดี คุณทดสอบ/)).toBeInTheDocument()
    })
  })

  it('renders service list after load', async () => {
    render(<BookingPage />)
    await waitFor(() => {
      expect(screen.getByText('นวดแผนไทย')).toBeInTheDocument()
      expect(screen.getByText('นวดน้ำมัน')).toBeInTheDocument()
    })
  })

  it('moves to duration step after selecting service', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('นวดแผนไทย'))
    expect(screen.getByText('เลือกระยะเวลา')).toBeInTheDocument()
    expect(screen.getByText('1 ชั่วโมง')).toBeInTheDocument()
    expect(screen.getByText('1.5 ชั่วโมง')).toBeInTheDocument()
    expect(screen.getByText('2 ชั่วโมง')).toBeInTheDocument()
  })

  it('moves to date step after selecting duration', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('1 ชั่วโมง'))
    expect(screen.getByText('เลือกวันที่')).toBeInTheDocument()
  })

  it('loads time slots after selecting date', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('1 ชั่วโมง'))

    const dateInput = screen.getByDisplayValue('')
    fireEvent.change(dateInput, { target: { value: '2025-06-02' } })
    fireEvent.click(screen.getByText('ถัดไป →'))

    await waitFor(() => {
      expect(screen.getByText('10:00')).toBeInTheDocument()
    })
  })

  it('disabled button shown for full slot', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('1 ชั่วโมง'))

    const dateInput = screen.getByDisplayValue('')
    fireEvent.change(dateInput, { target: { value: '2025-06-02' } })
    fireEvent.click(screen.getByText('ถัดไป →'))

    await waitFor(() => screen.getByText('เต็ม'))
    const fullSlot = screen.getByText('เต็ม').closest('button')
    expect(fullSlot).toBeDisabled()
  })

  it('completes full booking flow', async () => {
    render(<BookingPage />)

    // Step 1: service
    await waitFor(() => screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('นวดแผนไทย'))

    // Step 2: duration
    fireEvent.click(screen.getByText('1 ชั่วโมง'))

    // Step 3: date
    const dateInput = screen.getByDisplayValue('')
    fireEvent.change(dateInput, { target: { value: '2025-06-02' } })
    fireEvent.click(screen.getByText('ถัดไป →'))

    // Step 4: time
    await waitFor(() => screen.getByText('10:00'))
    fireEvent.click(screen.getByText('10:00').closest('button')!)

    // Step 5: confirm
    expect(screen.getByText('ยืนยันการจอง')).toBeInTheDocument()
    fireEvent.click(screen.getByText('✅ ยืนยันการจอง'))

    // Step 6: done
    await waitFor(() => {
      expect(screen.getByText('จองสำเร็จ!')).toBeInTheDocument()
    })
    expect(api.createBooking).toHaveBeenCalledWith(expect.objectContaining({
      lineUserId:      'U_TEST_123',
      serviceId:       'svc-1',
      startTime:       '10:00',
      durationMinutes: 60,
    }))
  })

  it('shows error alert when booking fails', async () => {
    ;(api.createBooking as jest.Mock).mockRejectedValueOnce({
      response: { data: { message: 'ช่วงเวลานี้เต็มแล้ว' } },
    })
    const alertMock = jest.spyOn(window, 'alert').mockImplementation(() => {})

    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('1 ชั่วโมง'))

    const dateInput = screen.getByDisplayValue('')
    fireEvent.change(dateInput, { target: { value: '2025-06-02' } })
    fireEvent.click(screen.getByText('ถัดไป →'))

    await waitFor(() => screen.getByText('10:00'))
    fireEvent.click(screen.getByText('10:00').closest('button')!)
    fireEvent.click(screen.getByText('✅ ยืนยันการจอง'))

    await waitFor(() => {
      expect(alertMock).toHaveBeenCalledWith('ช่วงเวลานี้เต็มแล้ว')
    })
    alertMock.mockRestore()
  })

  it('back button from duration returns to service step', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย'))
    fireEvent.click(screen.getByText('นวดแผนไทย'))
    expect(screen.getByText('เลือกระยะเวลา')).toBeInTheDocument()
    fireEvent.click(screen.getByText(/← กลับ/))
    expect(screen.getByText('เลือกบริการ')).toBeInTheDocument()
  })
})
