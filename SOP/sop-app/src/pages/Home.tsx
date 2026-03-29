import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Sun } from 'lucide-react'
import {
  ClipboardText, ChartLineUp, MagicWand,
  Pulse, ChartBar, Sparkle,
  CheckCircle, Warning, Clock, Lock, Play, CaretRight,
  Lightning, Circle, ArrowRight
} from '@phosphor-icons/react'
import { listMyWorkflows, listPublicWorkflows, authenticate, getToken, type WorkflowDefinition } from '../api/client'
import { Badge } from '../components/Badge'

// ── STAR Watch local API ────────────────────────────────────────────────
const SW_API = 'http://localhost:3001'

interface SwEvent {
  id: string; timestamp: string; source: string
  action: string; actor: string; entity: string; context: string
}
interface SwMatch {
  id: string; timestamp: string; source: string
  action: string; actor: string; stepName: string
  sopName: string; confidence: number; matchAction: string
}
interface SwStatus {
  startedAt: string; eventCount: number; matchCount: number
  connectors: string[]; lastEventAt: string | null
}

function sourceColor(src: string) {
  if (src === 'salesforce') return '#00A1E0'
  if (src === 'slack')      return '#4A154B'
  return '#2DD4BF'
}
function sourceLabel(src: string) {
  if (src === 'salesforce') return 'SF'
  if (src === 'slack')      return 'SL'
  return src.slice(0, 2).toUpperCase()
}
function actionLabel(action: string) {
  return action.replace(/_/g, ' ').replace(/\b\w/g, c => c.toUpperCase())
}
function timeAgo(iso: string) {
  const diff = Date.now() - new Date(iso).getTime()
  if (diff < 60_000)  return `${Math.round(diff / 1000)}s ago`
  if (diff < 3_600_000) return `${Math.round(diff / 60_000)}m ago`
  return `${Math.round(diff / 3_600_000)}h ago`
}

const RECENT_RUNS = [
  { id: 'run-1', sop: 'Enterprise Onboarding v3',     step: 'Go-live sign-off',      status: 'running',   avatar: 'Kelly A.',  elapsed: '2h 14m' },
  { id: 'run-2', sop: 'Container Gate-In & Customs',  step: 'EUDR Compliance Check', status: 'completed', avatar: 'System',    elapsed: '44m'    },
  { id: 'run-3', sop: 'DD Process — Fund Investment', step: 'IC Vote',               status: 'deviation', avatar: 'Max G.',    elapsed: '3d 1h'  },
  { id: 'run-4', sop: 'Monthly ESG Impact Report',    step: 'AI Analysis',            status: 'completed', avatar: 'System',    elapsed: '18m'    },
  { id: 'run-5', sop: 'New Employee Onboarding',      step: 'IT Provisioning',        status: 'running',   avatar: 'Jordan T.', elapsed: '6h 03m' },
]

function RunStatus({ status }: { status: string }) {
  if (status === 'running')   return <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: '0.82rem', color: '#2DD4BF', fontWeight: 500 }}><span style={{ width: 7, height: 7, borderRadius: '50%', background: '#2DD4BF', flexShrink: 0 }} />In progress</span>
  if (status === 'completed') return <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: '0.82rem', color: '#22C55E', fontWeight: 500 }}><CheckCircle size={13} weight="fill" />Completed</span>
  return <span style={{ display: 'inline-flex', alignItems: 'center', gap: 6, fontSize: '0.82rem', color: '#F59E0B', fontWeight: 500 }}><Warning size={13} weight="fill" />Deviation</span>
}

function Avatar({ name }: { name: string }) {
  const initials = name.split(' ').map(p => p[0]).join('')
  return (
    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8, fontSize: '0.875rem', color: '#A0A0A0' }}>
      <span style={{ width: 24, height: 24, borderRadius: '50%', background: '#1E1E1E', border: '1px solid #2E2E2E', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', fontSize: '0.6rem', fontWeight: 700, color: '#686868', flexShrink: 0 }}>
        {initials}
      </span>
      {name}
    </span>
  )
}

export function Home() {
  const navigate = useNavigate()
  const [workflows, setWorkflows] = useState<WorkflowDefinition[]>([])
  const [wfLoading, setWfLoading] = useState(false)
  const [authed, setAuthed] = useState(!!getToken())
  const [form, setForm] = useState({ username: '', password: '' })
  const [loginError, setLoginError] = useState('')

  // ── STAR Watch live feed ──────────────────────────────────────────────
  const [swEvents,  setSwEvents]  = useState<SwEvent[]>([])
  const [swMatches, setSwMatches] = useState<SwMatch[]>([])
  const [swStatus,  setSwStatus]  = useState<SwStatus | null>(null)
  const [swOnline,  setSwOnline]  = useState(false)
  const swTimer = useRef<ReturnType<typeof setInterval> | null>(null)

  useEffect(() => {
    async function poll() {
      try {
        const [evts, mts, sts] = await Promise.all([
          fetch(`${SW_API}/events?limit=12`).then(r => r.json()),
          fetch(`${SW_API}/matches?limit=6`).then(r => r.json()),
          fetch(`${SW_API}/status`).then(r => r.json()),
        ])
        setSwEvents(evts)
        setSwMatches(mts)
        setSwStatus(sts)
        setSwOnline(true)
      } catch {
        setSwOnline(false)
      }
    }
    poll()
    swTimer.current = setInterval(poll, 5000)
    return () => { if (swTimer.current) clearInterval(swTimer.current) }
  }, [])

  useEffect(() => {
    if (authed) {
      setWfLoading(true)
      Promise.all([listMyWorkflows(), listPublicWorkflows()])
        .then(([mine, pub]) => { const seen = new Set(mine.map(w => w.id)); setWorkflows([...mine, ...pub.filter(w => !seen.has(w.id))]) })
        .catch(() => {})
        .finally(() => setWfLoading(false))
    }
  }, [authed])

  async function handleLogin(e: React.FormEvent) {
    e.preventDefault(); setLoginError('')
    try { await authenticate(form.username, form.password); setAuthed(true) }
    catch { setLoginError('Invalid credentials. Try OASIS_ADMIN / Uppermall1!') }
  }

  return (
    <div style={{ padding: '48px 40px 64px' }}>

      {/* ── Page header — generous space, Instrument Serif display type ── */}
      <div className="fade-up fade-up-1" style={{ marginBottom: '48px' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
          <div style={{ width: 32, height: 32, borderRadius: 8, background: 'rgba(45,212,191,0.08)', border: '1px solid rgba(45,212,191,0.18)', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
            <Sun size={16} color="#2DD4BF" strokeWidth={1.5} />
          </div>
          <h1 style={{ fontFamily: 'var(--font-serif)', fontSize: '2.4rem', fontWeight: 400, color: '#DCDCDC', letterSpacing: '-0.01em', lineHeight: 1.15 }}>
            Good morning
          </h1>
        </div>
        <p style={{ fontSize: '1rem', color: '#636363', lineHeight: 1.65, maxWidth: 520 }}>
          Create, run, and audit operating procedures with AI guidance. Every completed run produces an immutable proof holon on STARNET.
        </p>
      </div>

      {/* ── Stats strip ── */}
      <div className="fade-up fade-up-2 panel-card" style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1px', background: 'rgba(255,255,255,0.06)', borderRadius: 10, overflow: 'hidden', marginBottom: '40px' }}>
        {[
          { label: 'Active SOPs',     value: 12,  sub: '8 yours · 4 forked',  trend: '↑ +2 this week', trendColor: '#2DD4BF', bar: 40  },
          { label: 'Runs This Month', value: 347, sub: 'Across all templates', trend: '94% complete',    trendColor: '#2DD4BF', bar: 94  },
          { label: 'Deviations',      value: 23,  sub: '8 unresolved',         trend: '↓ −15% vs last', trendColor: '#F59E0B', bar: 20  },
          { label: 'AI Improvements', value: 5,   sub: 'Awaiting your review', trend: null,              trendColor: '#8B5CF6', bar: null },
        ].map(s => (
          <div key={s.label} style={{ background: '#0A0A0A', padding: '24px 26px', position: 'relative' }}>
            <div style={{ fontSize: '0.68rem', color: '#484848', textTransform: 'uppercase', letterSpacing: '0.1em', fontWeight: 600, marginBottom: 14 }}>{s.label}</div>
            <div style={{ fontFamily: 'var(--font-serif)', fontSize: '2.6rem', fontWeight: 400, color: '#E0E0E0', letterSpacing: '-0.02em', lineHeight: 1, marginBottom: 12 }}>{s.value}</div>
            {/* Metric bar — borrowed from port-dashboard .mbar */}
            {s.bar !== null && (
              <div style={{ height: 2, background: 'rgba(255,255,255,0.06)', borderRadius: 1, marginBottom: 10, overflow: 'hidden' }}>
                <div style={{ height: '100%', width: `${s.bar}%`, background: `linear-gradient(90deg, ${s.trendColor}, transparent)`, borderRadius: 1 }} />
              </div>
            )}
            <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
              {s.trend && <span style={{ fontSize: '0.78rem', fontWeight: 600, color: s.trendColor }}>{s.trend}</span>}
              <span style={{ fontSize: '0.78rem', color: '#484848' }}>{s.sub}</span>
            </div>
          </div>
        ))}
      </div>

      {/* ── Get started — module cards ── */}
      <div style={{ marginBottom: '40px' }}>
        <h2 style={{ fontFamily: "'Instrument Serif', Georgia, serif", fontSize: '1.35rem', fontWeight: 400, color: '#DCDCDC', letterSpacing: '-0.01em', marginBottom: 4 }}>Get started</h2>
        <p style={{ fontSize: '0.875rem', color: '#525252', marginBottom: 20 }}>Choose a tool to begin.</p>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '12px' }}>
          {[
            {
              icon: <ClipboardText size={22} weight="bold" />,
              iconBg: 'rgba(45,212,191,0.08)',
              iconColor: '#2DD4BF',
              title: 'Run a SOP',
              label: 'SOPRunner',
              desc: 'Step through any procedure with live AI guidance. Sign off each step, upload evidence, and create an immutable audit trail.',
              action: 'Open SOPRunner →',
              path: '/runner',
            },
            {
              icon: <ChartLineUp size={22} weight="bold" />,
              iconBg: 'rgba(96,165,250,0.08)',
              iconColor: '#60A5FA',
              title: 'Review analytics',
              label: 'SOPIntel',
              desc: 'See which steps cause the most deviations, how long each takes, and get AI-generated suggestions to improve your procedures.',
              action: 'Open SOPIntel →',
              path: '/intel',
            },
            {
              icon: <MagicWand size={22} weight="bold" />,
              iconBg: 'rgba(139,92,246,0.08)',
              iconColor: '#8B5CF6',
              title: 'Create a new SOP',
              label: 'AI Authoring',
              desc: 'Describe a process in plain English. BRAID AI drafts the full SOP with steps, roles, connectors, and decision points.',
              action: 'Start with AI →',
              path: '/authoring',
            },
          ].map((m, i) => (
            <button
              key={m.path}
              onClick={() => navigate(m.path)}
              className={`fade-up fade-up-${i + 3}`}
              style={{ background: '#161616', border: '1px solid rgba(255,255,255,0.07)', borderRadius: 10, padding: '24px 26px', textAlign: 'left', cursor: 'pointer', transition: 'border-color 0.15s, background 0.15s', display: 'flex', flexDirection: 'column', gap: 16 }}
              onMouseEnter={e => { e.currentTarget.style.borderColor = 'rgba(45,212,191,0.2)'; e.currentTarget.style.background = '#151515' }}
              onMouseLeave={e => { e.currentTarget.style.borderColor = 'rgba(255,255,255,0.07)'; e.currentTarget.style.background = 'transparent' }}
            >
              <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                <div style={{ width: 40, height: 40, borderRadius: 9, background: m.iconBg, border: `0.5px solid ${m.iconColor}30`, display: 'flex', alignItems: 'center', justifyContent: 'center', color: m.iconColor }}>
                  {m.icon}
                </div>
                <span style={{ fontSize: '0.72rem', color: '#484848', fontWeight: 500, background: '#1E1E1E', border: '1px solid #2E2E2E', padding: '2px 8px', borderRadius: 4 }}>{m.label}</span>
              </div>
              <div>
                <div style={{ fontFamily: "'Instrument Serif', Georgia, serif", fontSize: '1.15rem', fontWeight: 400, color: '#DCDCDC', marginBottom: 8, lineHeight: 1.2 }}>{m.title}</div>
                <div style={{ fontSize: '0.875rem', color: '#636363', lineHeight: 1.65 }}>{m.desc}</div>
              </div>
              <span style={{ fontSize: '0.82rem', color: '#2DD4BF', fontWeight: 500, opacity: 0.7 }}>{m.action}</span>
            </button>
          ))}
        </div>
      </div>

      {/* ── Recent runs ── */}
      <div style={{ marginBottom: '32px' }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 20 }}>
          <div>
            <h2 style={{ fontFamily: "'Instrument Serif', Georgia, serif", fontSize: '1.35rem', fontWeight: 400, color: '#DCDCDC', letterSpacing: '-0.01em', marginBottom: 4 }}>Recent runs</h2>
            <p style={{ fontSize: '0.875rem', color: '#525252' }}>Live status across active SOP executions</p>
          </div>
          <button className="btn-primary" onClick={() => navigate('/runner')} style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
            <Play size={12} weight="fill" /> New Run
          </button>
        </div>

        <div className="panel-card" style={{ borderRadius: 10, overflow: 'hidden', background: '#111111' }}>
          {/* Table header */}
          <div style={{ display: 'grid', gridTemplateColumns: '2fr 1.5fr 130px 110px 150px', padding: '10px 20px', borderBottom: '1px solid rgba(255,255,255,0.06)', background: 'rgba(0,0,0,0.2)' }}>
            {['SOP Name', 'Current Step', 'Assigned To', 'Elapsed', 'Status'].map(h => (
              <span key={h} style={{ fontSize: '0.68rem', color: '#484848', textTransform: 'uppercase', letterSpacing: '0.09em', fontWeight: 600 }}>{h}</span>
            ))}
          </div>

          {RECENT_RUNS.map((run, i) => (
            <div
              key={run.id}
              onClick={() => navigate(`/runner/${run.id}`)}
              style={{ display: 'grid', gridTemplateColumns: '2fr 1.5fr 130px 110px 150px', padding: '0 20px', alignItems: 'center', height: 54, borderBottom: i < RECENT_RUNS.length - 1 ? '1px solid rgba(255,255,255,0.04)' : 'none', cursor: 'pointer', transition: 'background 0.1s' }}
              onMouseEnter={e => (e.currentTarget as HTMLDivElement).style.background = 'rgba(255,255,255,0.025)'}
              onMouseLeave={e => (e.currentTarget as HTMLDivElement).style.background = 'transparent'}
            >
              <span style={{ fontSize: '0.9rem', color: '#C4C4C4', fontWeight: 500, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', paddingRight: 16 }}>{run.sop}</span>
              <span style={{ fontSize: '0.85rem', color: '#686868', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap', paddingRight: 16 }}>{run.step}</span>
              <Avatar name={run.avatar} />
              <span style={{ display: 'flex', alignItems: 'center', gap: 5, fontSize: '0.82rem', color: '#525252', fontFamily: 'var(--font-mono)' }}>
                <Clock size={12} weight="regular" /> {run.elapsed}
              </span>
              <RunStatus status={run.status} />
            </div>
          ))}
        </div>
      </div>

      {/* ── STARNET Library ── */}
      <div>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 20 }}>
          <div>
            <h2 style={{ fontFamily: "'Instrument Serif', Georgia, serif", fontSize: '1.35rem', fontWeight: 400, color: '#DCDCDC', letterSpacing: '-0.01em', marginBottom: 4 }}>STARNET Library</h2>
            <p style={{ fontSize: '0.875rem', color: '#525252' }}>Discover and fork published SOP templates from the OASIS network</p>
          </div>
          {!authed && (
            <span style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.82rem', color: '#F59E0B', fontWeight: 500 }}>
              <Lock size={13} weight="bold" /> Sign in to load live templates
            </span>
          )}
        </div>

        <div className="panel-card" style={{ borderRadius: 10, overflow: 'hidden', background: '#111111' }}>
          {!authed ? (
            <div style={{ padding: '24px 24px' }}>
              <p style={{ fontSize: '0.875rem', color: '#636363', marginBottom: 16 }}>Connect to STARNET to browse and fork published SOP templates from the OASIS community.</p>
              <form onSubmit={handleLogin} style={{ display: 'flex', gap: 10, alignItems: 'flex-end', flexWrap: 'wrap', maxWidth: 520 }}>
                <div style={{ flex: 1, minWidth: 160 }}>
                  <label style={{ display: 'block', fontSize: '0.8rem', color: '#686868', marginBottom: 6, fontWeight: 500 }}>Username</label>
                  <input value={form.username} onChange={e => setForm(f => ({ ...f, username: e.target.value }))} placeholder="OASIS_ADMIN" />
                </div>
                <div style={{ flex: 1, minWidth: 160 }}>
                  <label style={{ display: 'block', fontSize: '0.8rem', color: '#686868', marginBottom: 6, fontWeight: 500 }}>Password</label>
                  <input type="password" value={form.password} onChange={e => setForm(f => ({ ...f, password: e.target.value }))} placeholder="••••••••" />
                </div>
                <button type="submit" className="btn-primary" style={{ padding: '9px 18px' }}>Connect to STARNET</button>
                {loginError && <p style={{ width: '100%', fontSize: '0.82rem', color: '#EF4444' }}>{loginError}</p>}
              </form>
            </div>
          ) : wfLoading ? (
            <div style={{ padding: '24px', color: '#525252', fontSize: '0.875rem' }}>Loading workflows…</div>
          ) : workflows.length === 0 ? (
            <div style={{ padding: '24px', color: '#525252', fontSize: '0.875rem' }}>No workflows found. Create one in AI Authoring or the Workflow Builder.</div>
          ) : (
            workflows.slice(0, 8).map((wf, i) => (
              <div
                key={wf.id}
                onClick={() => navigate(`/runner/${wf.id}`)}
                style={{ display: 'flex', alignItems: 'center', gap: 14, padding: '0 20px', height: 58, borderBottom: i < Math.min(workflows.length, 8) - 1 ? '1px solid rgba(255,255,255,0.04)' : 'none', cursor: 'pointer', transition: 'background 0.1s' }}
                onMouseEnter={e => (e.currentTarget as HTMLDivElement).style.background = 'rgba(45,212,191,0.025)'}
                onMouseLeave={e => (e.currentTarget as HTMLDivElement).style.background = 'transparent'}
              >
                <div style={{ width: 32, height: 32, borderRadius: 6, background: 'rgba(45,212,191,0.06)', border: '1px solid rgba(45,212,191,0.15)', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, fontSize: '0.55rem', fontWeight: 700, color: '#2DD4BF', letterSpacing: '0.02em' }}>SOP</div>
                <div style={{ flex: 1, overflow: 'hidden' }}>
                  <div style={{ fontSize: '0.9rem', fontWeight: 500, color: '#C4C4C4', marginBottom: 2, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{wf.name}</div>
                  <div style={{ fontSize: '0.8rem', color: '#525252', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{wf.description}</div>
                </div>
                <div style={{ display: 'flex', alignItems: 'center', gap: 8, flexShrink: 0 }}>
                  <Badge variant="active">Run SOP</Badge>
                  <CaretRight size={14} weight="bold" color="#484848" />
                </div>
              </div>
            ))
          )}
        </div>
      </div>

      {/* ── STAR Watch Live Feed ───────────────────────────────────────── */}
      <div className="fade-up fade-up-4" style={{ marginTop: 40 }}>
        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: 16 }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <div style={{ width: 28, height: 28, borderRadius: 7, background: 'rgba(45,212,191,0.08)', border: '1px solid rgba(45,212,191,0.18)', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
              <Lightning size={14} weight="fill" color="#2DD4BF" />
            </div>
            <h2 style={{ fontFamily: 'var(--font-sans)', fontWeight: 600, fontSize: '1rem', color: '#C4C4C4', letterSpacing: '-0.01em' }}>
              STAR Watch — Live Activity
            </h2>
          </div>
          <div style={{ display: 'flex', alignItems: 'center', gap: 6, fontSize: '0.78rem', color: swOnline ? '#2DD4BF' : '#525252' }}>
            <Circle size={7} weight="fill" color={swOnline ? '#2DD4BF' : '#404040'} />
            {swOnline
              ? `${swStatus?.connectors?.join(' + ') ?? 'connected'} · ${swStatus?.eventCount ?? 0} events`
              : 'star-watch not running'}
          </div>
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 360px', gap: 16 }}>

          {/* Event stream */}
          <div className="panel-card" style={{ padding: 0, overflow: 'hidden' }}>
            <div style={{ padding: '12px 18px', borderBottom: '0.5px solid rgba(255,255,255,0.06)', fontSize: '0.78rem', fontWeight: 600, color: '#525252', letterSpacing: '0.06em', textTransform: 'uppercase' }}>
              Detected Events
            </div>
            {!swOnline ? (
              <div style={{ padding: '32px 20px', textAlign: 'center', color: '#404040', fontSize: '0.85rem' }}>
                <Pulse size={28} style={{ marginBottom: 10, opacity: 0.3 }} />
                <div>Run <code style={{ fontSize: '0.82rem', color: '#686868', background: 'rgba(255,255,255,0.04)', padding: '2px 6px', borderRadius: 4 }}>npm run watch</code> in <code style={{ fontSize: '0.82rem', color: '#686868', background: 'rgba(255,255,255,0.04)', padding: '2px 6px', borderRadius: 4 }}>SOP/star-watch</code> to connect</div>
              </div>
            ) : swEvents.length === 0 ? (
              <div style={{ padding: '32px 20px', textAlign: 'center', color: '#404040', fontSize: '0.85rem' }}>
                Listening for events from {swStatus?.connectors?.join(', ') ?? 'connectors'}…
              </div>
            ) : swEvents.map((ev, i) => (
              <div key={ev.id} style={{ display: 'flex', alignItems: 'center', gap: 12, padding: '10px 18px', borderBottom: i < swEvents.length - 1 ? '0.5px solid rgba(255,255,255,0.04)' : 'none' }}>
                {/* Source badge */}
                <div style={{ width: 30, height: 30, borderRadius: 7, background: `${sourceColor(ev.source)}18`, border: `0.5px solid ${sourceColor(ev.source)}40`, display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0, fontSize: '0.58rem', fontWeight: 800, color: sourceColor(ev.source), letterSpacing: '0.02em' }}>
                  {sourceLabel(ev.source)}
                </div>
                <div style={{ flex: 1, overflow: 'hidden' }}>
                  <div style={{ fontSize: '0.875rem', fontWeight: 500, color: '#C4C4C4', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                    {ev.entity}
                  </div>
                  <div style={{ fontSize: '0.78rem', color: '#525252', marginTop: 1 }}>
                    {actionLabel(ev.action)} · {ev.actor}
                  </div>
                </div>
                <div style={{ fontSize: '0.72rem', color: '#404040', flexShrink: 0 }}>{timeAgo(ev.timestamp)}</div>
              </div>
            ))}
          </div>

          {/* Matched steps */}
          <div className="panel-card" style={{ padding: 0, overflow: 'hidden' }}>
            <div style={{ padding: '12px 18px', borderBottom: '0.5px solid rgba(255,255,255,0.06)', fontSize: '0.78rem', fontWeight: 600, color: '#525252', letterSpacing: '0.06em', textTransform: 'uppercase' }}>
              SOP Matches
            </div>
            {!swOnline || swMatches.length === 0 ? (
              <div style={{ padding: '32px 16px', textAlign: 'center', color: '#404040', fontSize: '0.82rem' }}>
                {swOnline ? 'No matches yet — events will auto-match to SOP steps' : '—'}
              </div>
            ) : swMatches.map((m, i) => (
              <div key={m.id} style={{ padding: '12px 16px', borderBottom: i < swMatches.length - 1 ? '0.5px solid rgba(255,255,255,0.04)' : 'none' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: 6, marginBottom: 4 }}>
                  <div style={{ width: 22, height: 22, borderRadius: 5, background: `${sourceColor(m.source)}18`, display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '0.52rem', fontWeight: 800, color: sourceColor(m.source), flexShrink: 0 }}>
                    {sourceLabel(m.source)}
                  </div>
                  <ArrowRight size={10} color="#404040" />
                  <span style={{ fontSize: '0.8rem', fontWeight: 600, color: '#C4C4C4', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{m.stepName}</span>
                </div>
                <div style={{ fontSize: '0.75rem', color: '#525252', marginBottom: 4 }}>{m.sopName}</div>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                  <span style={{ fontSize: '0.7rem', color: m.matchAction === 'auto_complete' ? '#2DD4BF' : '#F59E0B', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>
                    {m.matchAction === 'auto_complete' ? 'Auto-completed' : 'Escalated'}
                  </span>
                  <span style={{ fontSize: '0.7rem', color: '#404040' }}>{Math.round(m.confidence * 100)}% match · {timeAgo(m.timestamp)}</span>
                </div>
              </div>
            ))}
          </div>

        </div>
      </div>

    </div>
  )
}
