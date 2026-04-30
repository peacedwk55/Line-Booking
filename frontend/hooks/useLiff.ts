// hooks/useLiff.ts
import { useEffect, useState } from 'react'

export interface LiffProfile {
  userId: string
  displayName: string
  pictureUrl?: string
}

export function useLiff() {
  const [profile, setProfile]   = useState<LiffProfile | null>(null)
  const [ready,   setReady]     = useState(false)
  const [error,   setError]     = useState<string | null>(null)

  useEffect(() => {
    const init = async () => {
      if (process.env.NEXT_PUBLIC_LIFF_MOCK === 'true') {
        setProfile({ userId: 'U_DEV_001', displayName: 'Dev User', pictureUrl: undefined })
        setReady(true)
        return
      }

      try {
        const liff = (await import('@line/liff')).default
        await liff.init({ liffId: process.env.NEXT_PUBLIC_LIFF_ID! })

        if (!liff.isLoggedIn()) {
          liff.login()
          return
        }

        const p = await liff.getProfile()
        setProfile({
          userId:      p.userId,
          displayName: p.displayName,
          pictureUrl:  p.pictureUrl
        })
        setReady(true)
      } catch (e) {
        setError('LINE login failed')
        console.error(e)
      }
    }
    init()
  }, [])

  return { profile, ready, error }
}
