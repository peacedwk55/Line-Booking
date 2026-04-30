// __tests__/MyBookingsPage.test.tsx
import { render, screen, waitFor, fireEvent } from '@testing-library/react'
import MyBookingsPage from '@/app/my-bookings/page'
import * as api from '@/lib/api'
import * as liff from '@/hooks/useLiff'

jest.mock('@/hooks/useLiff', () => ({ useLiff: jest.fn() }))
jest.mock('@/lib/api', () => ({
  getUserBookings: jest.fn(),
  cancelBooking:   jest.fn(),
}))
jest.mock('next/link', () => ({
  __esModule: true,
  default: ({ children, href }: any) => <a href={href}>{children}</a>,
}))

const mockProfile = { userId: 'U_ME', displayName: 'ทดสอบ', pictureUrl: undefined }

const confirmed: api.Booking = {
  id: 'bk-1', date: '2025-06-10', startTime: '10:00',
  endTime: '11:30', service: 'นวดแผนไทย 60 นาที',
  status: 'Confirmed', createdAt: '2025-06-01T00:00:00Z',
}
const cancelled: api.Booking = {
  id: 'bk-2', date: '2025-06-05', startTime: '14:00',
  endTime: '15:30', service: 'นวดน้ำมัน 60 นาที',
  status: 'Cancelled', createdAt: '2025-06-01T00:00:00Z',
}

function setup(bookings: api.Booking[] = []) {
  ;(liff.useLiff as jest.Mock).mockReturnValue({ profile: mockProfile, ready: true, error: null })
  ;(api.getUserBookings as jest.Mock).mockResolvedValue(bookings)
}

describe('MyBookingsPage', () => {
  beforeEach(() => jest.clearAllMocks())

  it('shows loading spinner while not ready', () => {
    ;(liff.useLiff as jest.Mock).mockReturnValue({ profile: null, ready: false, error: null })
    ;(api.getUserBookings as jest.Mock).mockResolvedValue([])
    render(<MyBookingsPage />)
    expect(document.querySelector('.animate-spin')).toBeInTheDocument()
  })

  it('shows empty state when no bookings', async () => {
    setup([])
    render(<MyBookingsPage />)
    await waitFor(() => expect(screen.getByText('ยังไม่มีการจอง')).toBeInTheDocument())
  })

  it('renders confirmed booking in upcoming section', async () => {
    setup([confirmed])
    render(<MyBookingsPage />)
    await waitFor(() => {
      expect(screen.getByText('นัดที่กำลังจะมาถึง')).toBeInTheDocument()
      expect(screen.getByText('นวดแผนไทย 60 นาที')).toBeInTheDocument()
      expect(screen.getByText('ยืนยันแล้ว')).toBeInTheDocument()
    })
  })

  it('renders cancelled booking in history section', async () => {
    setup([cancelled])
    render(<MyBookingsPage />)
    await waitFor(() => {
      expect(screen.getByText('ประวัติ')).toBeInTheDocument()
      expect(screen.getByText('ยกเลิกแล้ว')).toBeInTheDocument()
    })
  })

  it('shows cancel button only on confirmed booking', async () => {
    setup([confirmed, cancelled])
    render(<MyBookingsPage />)
    await waitFor(() => screen.getByText('ยกเลิกการจอง'))
    const cancelBtns = screen.getAllByText('ยกเลิกการจอง')
    expect(cancelBtns).toHaveLength(1)  // only for confirmed
  })

  it('calls cancelBooking and reloads on confirm', async () => {
    setup([confirmed])
    ;(api.getUserBookings as jest.Mock)
      .mockResolvedValueOnce([confirmed])
      .mockResolvedValueOnce([])        // after cancel, empty
    ;(api.cancelBooking as jest.Mock).mockResolvedValue({ message: 'ยกเลิกสำเร็จ' })

    jest.spyOn(window, 'confirm').mockReturnValue(true)
    render(<MyBookingsPage />)
    await waitFor(() => screen.getByText('ยกเลิกการจอง'))
    fireEvent.click(screen.getByText('ยกเลิกการจอง'))

    await waitFor(() => {
      expect(api.cancelBooking).toHaveBeenCalledWith('bk-1', 'U_ME')
      expect(api.getUserBookings).toHaveBeenCalledTimes(2)
    })
  })

  it('does NOT cancel when user dismisses confirm dialog', async () => {
    setup([confirmed])
    jest.spyOn(window, 'confirm').mockReturnValue(false)
    render(<MyBookingsPage />)
    await waitFor(() => screen.getByText('ยกเลิกการจอง'))
    fireEvent.click(screen.getByText('ยกเลิกการจอง'))
    expect(api.cancelBooking).not.toHaveBeenCalled()
  })

  it('shows alert when cancel API fails', async () => {
    setup([confirmed])
    ;(api.cancelBooking as jest.Mock).mockRejectedValue({
      response: { data: { message: 'ไม่สามารถยกเลิกได้ภายใน 2 ชั่วโมงก่อนนัด' } },
    })
    jest.spyOn(window, 'confirm').mockReturnValue(true)
    const alertMock = jest.spyOn(window, 'alert').mockImplementation(() => {})
    render(<MyBookingsPage />)
    await waitFor(() => screen.getByText('ยกเลิกการจอง'))
    fireEvent.click(screen.getByText('ยกเลิกการจอง'))
    await waitFor(() =>
      expect(alertMock).toHaveBeenCalledWith('ไม่สามารถยกเลิกได้ภายใน 2 ชั่วโมงก่อนนัด')
    )
    alertMock.mockRestore()
  })

  it('separates upcoming and history correctly', async () => {
    setup([confirmed, cancelled])
    render(<MyBookingsPage />)
    await waitFor(() => {
      expect(screen.getByText('นัดที่กำลังจะมาถึง')).toBeInTheDocument()
      expect(screen.getByText('ประวัติ')).toBeInTheDocument()
    })
  })
})
