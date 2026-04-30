// __tests__/api.test.ts  —  Unit tests for API client + booking utils
import {
  getServices,
  getSlots,
  createBooking,
  getUserBookings,
  cancelBooking,
} from '@/lib/api'

// Use var (not let/const) — var is initialized before TDZ, required for jest.mock factory hoisting with SWC
// eslint-disable-next-line no-var
var mockInstance: { get: jest.Mock; post: jest.Mock; delete: jest.Mock }

jest.mock('axios', () => {
  // This runs lazily when axios is first imported — mockInstance is in scope here
  mockInstance = {
    get:    jest.fn(),
    post:   jest.fn(),
    delete: jest.fn(),
  }
  return {
    __esModule: true,
    default: { create: () => mockInstance },
    create:  () => mockInstance,
  }
})

beforeEach(() => {
  mockInstance.get.mockReset()
  mockInstance.post.mockReset()
  mockInstance.delete.mockReset()
})

// ── getServices ────────────────────────────────────────────────────

describe('getServices', () => {
  it('returns service list on success', async () => {
    const mockServices = [
      { id: 'svc-1', name: 'นวดแผนไทย 60 นาที', durationMinutes: 60, price: 350 },
    ]
    mockInstance.get.mockResolvedValueOnce({ data: mockServices })

    const result = await getServices()

    expect(mockInstance.get).toHaveBeenCalledWith('/services')
    expect(result).toEqual(mockServices)
  })

  it('propagates network error', async () => {
    mockInstance.get.mockRejectedValueOnce(new Error('Network error'))
    await expect(getServices()).rejects.toThrow('Network error')
  })
})

// ── getSlots ───────────────────────────────────────────────────────

describe('getSlots', () => {
  it('calls correct endpoint with date param', async () => {
    const mockSlots = [
      { start: '10:00', end: '11:30', available: true, remaining: 2 },
      { start: '14:00', end: '15:30', available: false, remaining: 0 },
    ]
    mockInstance.get.mockResolvedValueOnce({ data: mockSlots })

    const result = await getSlots('2025-06-02')

    expect(mockInstance.get).toHaveBeenCalledWith('/bookings/slots?date=2025-06-02')
    expect(result).toHaveLength(2)
    expect(result[0].available).toBe(true)
    expect(result[1].available).toBe(false)
  })
})

// ── createBooking ──────────────────────────────────────────────────

describe('createBooking', () => {
  const payload = {
    lineUserId: 'U123',
    serviceId:  'svc-1',
    date:       '2025-06-02',
    startTime:  '10:00',
    endTime:    '11:30',
  }

  it('posts payload and returns booking id', async () => {
    const mockResponse = { bookingId: 'bk-uuid', message: 'จองสำเร็จ!' }
    mockInstance.post.mockResolvedValueOnce({ data: mockResponse })

    const result = await createBooking(payload)

    expect(mockInstance.post).toHaveBeenCalledWith('/bookings', payload)
    expect(result.bookingId).toBe('bk-uuid')
  })

  it('throws on 400 response (slot full)', async () => {
    mockInstance.post.mockRejectedValueOnce({
      response: { status: 400, data: { message: 'ช่วงเวลานี้เต็มแล้ว' } },
    })
    await expect(createBooking(payload)).rejects.toMatchObject({
      response: { data: { message: 'ช่วงเวลานี้เต็มแล้ว' } },
    })
  })
})

// ── getUserBookings ────────────────────────────────────────────────

describe('getUserBookings', () => {
  it('passes lineUserId as query param', async () => {
    mockInstance.get.mockResolvedValueOnce({ data: [] })

    await getUserBookings('U_ABC')

    expect(mockInstance.get).toHaveBeenCalledWith('/bookings?lineUserId=U_ABC')
  })

  it('returns sorted booking list', async () => {
    const bookings = [
      { id: 'b1', date: '2025-06-10', startTime: '10:00', status: 'Confirmed' },
      { id: 'b2', date: '2025-06-02', startTime: '14:00', status: 'Cancelled' },
    ]
    mockInstance.get.mockResolvedValueOnce({ data: bookings })

    const result = await getUserBookings('U_ABC')
    expect(result).toHaveLength(2)
  })
})

// ── cancelBooking ──────────────────────────────────────────────────

describe('cancelBooking', () => {
  it('calls delete with id and lineUserId', async () => {
    mockInstance.delete.mockResolvedValueOnce({ data: { message: 'ยกเลิกสำเร็จ' } })

    await cancelBooking('bk-uuid', 'U_123')

    expect(mockInstance.delete).toHaveBeenCalledWith(
      '/bookings/bk-uuid?lineUserId=U_123'
    )
  })
})
