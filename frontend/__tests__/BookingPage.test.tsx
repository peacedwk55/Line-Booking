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
  { id: 'svc-1', name: 'นวดแผนไทย 60 นาที', description: 'ผ่อนคลาย', durationMinutes: 60, price: 350 },
  { id: 'svc-2', name: 'นวดน้ำมัน 60 นาที', description: 'อโรมา', durationMinutes: 60, price: 450 },
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
      expect(screen.getByText('นวดแผนไทย 60 นาที')).toBeInTheDocument()
      expect(screen.getByText('นวดน้ำมัน 60 นาที')).toBeInTheDocument()
    })
  })

  it('moves to date step after selecting service', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย 60 นาที'))

    fireEvent.click(screen.getByText('นวดแผนไทย 60 นาที'))

    expect(screen.getByText('เลือกวันที่')).toBeInTheDocument()
    expect(screen.getByText('นวดแผนไทย 60 นาที', { selector: 'p' })).toBeInTheDocument()
  })

  it('loads time slots after selecting date', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย 60 นาที'))
    fireEvent.click(screen.getByText('นวดแผนไทย 60 นาที'))

    // Click first available date
    const dateButtons = screen.getAllByRole('button').filter(b =>
      b.closest('.grid') && b.textContent?.match(/\d+/)
    )
    fireEvent.click(dateButtons[0])

    await waitFor(() => {
      expect(screen.getByText(/10:00 – 11:30/)).toBeInTheDocument()
      expect(screen.getByText(/14:00 – 15:30/)).toBeInTheDocument()
    })
  })

  it('disabled button shown for full slot', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย 60 นาที'))
    fireEvent.click(screen.getByText('นวดแผนไทย 60 นาที'))

    const dateButtons = screen.getAllByRole('button').filter(b =>
      b.textContent?.match(/^\D*\d{1,2}\D*$/)
    )
    fireEvent.click(dateButtons[0])

    await waitFor(() => screen.getByText('เต็ม'))
    const fullSlot = screen.getByText('เต็ม').closest('button')
    expect(fullSlot).toBeDisabled()
  })

  it('completes full booking flow', async () => {
    render(<BookingPage />)

    // Step 1: service
    await waitFor(() => screen.getByText('นวดแผนไทย 60 นาที'))
    fireEvent.click(screen.getByText('นวดแผนไทย 60 นาที'))

    // Step 2: date
    const dateButtons = screen.getAllByRole('button').filter(b =>
      b.textContent?.match(/\d+/) && !b.textContent?.includes('←')
    )
    fireEvent.click(dateButtons[0])

    // Step 3: time
    await waitFor(() => screen.getByText(/10:00 – 11:30/))
    fireEvent.click(screen.getByText(/10:00 – 11:30/).closest('button')!)

    // Step 4: confirm
    expect(screen.getByText('ยืนยันการจอง')).toBeInTheDocument()
    fireEvent.click(screen.getByText('✅ ยืนยันการจอง'))

    // Step 5: done
    await waitFor(() => {
      expect(screen.getByText('จองสำเร็จ!')).toBeInTheDocument()
    })
    expect(api.createBooking).toHaveBeenCalledWith(expect.objectContaining({
      lineUserId: 'U_TEST_123',
      serviceId:  'svc-1',
      startTime:  '10:00',
    }))
  })

  it('shows error alert when booking fails', async () => {
    ;(api.createBooking as jest.Mock).mockRejectedValueOnce({
      response: { data: { message: 'ช่วงเวลานี้เต็มแล้ว' } },
    })
    const alertMock = jest.spyOn(window, 'alert').mockImplementation(() => {})

    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย 60 นาที'))
    fireEvent.click(screen.getByText('นวดแผนไทย 60 นาที'))

    const dateButtons = screen.getAllByRole('button').filter(b => b.textContent?.match(/\d+/))
    fireEvent.click(dateButtons[0])

    await waitFor(() => screen.getByText(/10:00 – 11:30/))
    fireEvent.click(screen.getByText(/10:00 – 11:30/).closest('button')!)
    fireEvent.click(screen.getByText('✅ ยืนยันการจอง'))

    await waitFor(() => {
      expect(alertMock).toHaveBeenCalledWith('ช่วงเวลานี้เต็มแล้ว')
    })
    alertMock.mockRestore()
  })

  it('back button returns to previous step', async () => {
    render(<BookingPage />)
    await waitFor(() => screen.getByText('นวดแผนไทย 60 นาที'))
    fireEvent.click(screen.getByText('นวดแผนไทย 60 นาที'))

    expect(screen.getByText('เลือกวันที่')).toBeInTheDocument()
    fireEvent.click(screen.getByText(/← กลับ/))
    expect(screen.getByText('เลือกบริการ')).toBeInTheDocument()
  })
})
