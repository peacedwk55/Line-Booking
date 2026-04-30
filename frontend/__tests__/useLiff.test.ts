// __tests__/useLiff.test.ts
import { renderHook, waitFor } from '@testing-library/react'
import { useLiff } from '@/hooks/useLiff'

// Define mock inside factory — avoids hoisting/TDZ issues
jest.mock('@line/liff', () => ({
  __esModule: true,
  default: {
    init:       jest.fn(),
    isLoggedIn: jest.fn(),
    login:      jest.fn(),
    getProfile: jest.fn(),
  },
}))

// Access the mock after module is registered
// eslint-disable-next-line @typescript-eslint/no-var-requires
const mockLiff = (jest.requireMock('@line/liff') as { default: {
  init:       jest.Mock
  isLoggedIn: jest.Mock
  login:      jest.Mock
  getProfile: jest.Mock
} }).default

// Set LIFF env var
process.env.NEXT_PUBLIC_LIFF_ID = 'test-liff-id'

describe('useLiff', () => {
  beforeEach(() => jest.clearAllMocks())

  it('returns profile after successful init and login', async () => {
    mockLiff.init.mockResolvedValue(undefined)
    mockLiff.isLoggedIn.mockReturnValue(true)
    mockLiff.getProfile.mockResolvedValue({
      userId:      'U_HOOK_TEST',
      displayName: 'Hook User',
      pictureUrl:  'https://example.com/pic.jpg',
    })

    const { result } = renderHook(() => useLiff())

    await waitFor(() => expect(result.current.ready).toBe(true))

    expect(result.current.profile).toEqual({
      userId:      'U_HOOK_TEST',
      displayName: 'Hook User',
      pictureUrl:  'https://example.com/pic.jpg',
    })
    expect(result.current.error).toBeNull()
  })

  it('calls liff.login() if not logged in', async () => {
    mockLiff.init.mockResolvedValue(undefined)
    mockLiff.isLoggedIn.mockReturnValue(false)

    renderHook(() => useLiff())

    await waitFor(() => expect(mockLiff.login).toHaveBeenCalled())
  })

  it('sets error state when init throws', async () => {
    mockLiff.init.mockRejectedValue(new Error('LIFF init failed'))

    const { result } = renderHook(() => useLiff())

    await waitFor(() => expect(result.current.error).toBe('LINE login failed'))
    expect(result.current.ready).toBe(false)
    expect(result.current.profile).toBeNull()
  })

  it('starts with ready=false and profile=null', () => {
    // Don't resolve init — keep it pending
    mockLiff.init.mockReturnValue(new Promise(() => {}))

    const { result } = renderHook(() => useLiff())

    expect(result.current.ready).toBe(false)
    expect(result.current.profile).toBeNull()
    expect(result.current.error).toBeNull()
  })

  it('passes correct liffId to init', async () => {
    mockLiff.init.mockResolvedValue(undefined)
    mockLiff.isLoggedIn.mockReturnValue(true)
    mockLiff.getProfile.mockResolvedValue({
      userId: 'U_ID', displayName: 'Name', pictureUrl: undefined,
    })

    renderHook(() => useLiff())

    await waitFor(() => expect(mockLiff.init).toHaveBeenCalledWith({
      liffId: 'test-liff-id',
    }))
  })
})
