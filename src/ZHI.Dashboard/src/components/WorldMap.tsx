import { useRef, useEffect, useCallback, useState } from 'react'
import type { AgentSnapshot, FoodTile, CorpseTile } from '../types'

const GRID_W = 64
const GRID_H = 64
const MIN_ZOOM = 0.5
const MAX_ZOOM = 12

interface Props {
  agents: AgentSnapshot[]
  food: FoodTile[]
  corpses: CorpseTile[]
  trackedAgent?: number | null
  onTrackChange?: (id: number | null) => void
}

interface TooltipInfo {
  text: string[]
  x: number
  y: number
}

export function WorldMap({ agents, food, corpses, trackedAgent: trackedProp, onTrackChange }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null)
  const camRef = useRef({ x: 0, y: 0, zoom: 1 })
  const dragRef = useRef({ dragging: false, lastX: 0, lastY: 0 })
  const [internalTracked, setInternalTracked] = useState<number | null>(null)
  const [tooltip, setTooltip] = useState<TooltipInfo | null>(null)
  const rafRef = useRef<number>(0)

  const trackedAgent = trackedProp !== undefined ? trackedProp : internalTracked
  const setTrackedAgent = onTrackChange ?? setInternalTracked

  const draw = useCallback(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    const rect = canvas.getBoundingClientRect()
    const dpr = window.devicePixelRatio || 1
    canvas.width = rect.width * dpr
    canvas.height = rect.height * dpr
    ctx.scale(dpr, dpr)

    const w = rect.width
    const h = rect.height
    const cam = camRef.current
    const cellSize = cam.zoom * (w / GRID_W)

    // Track agent: center camera on it
    if (trackedAgent !== null) {
      const agent = agents.find(a => a.id === trackedAgent && a.is_alive)
      if (agent) {
        cam.x = agent.x * cellSize + cellSize / 2 - w / 2
        cam.y = agent.y * cellSize + cellSize / 2 - h / 2
      }
    }

    ctx.fillStyle = '#0a0a0a'
    ctx.fillRect(0, 0, w, h)

    ctx.save()
    ctx.translate(-cam.x, -cam.y)

    // Grid background
    const totalW = GRID_W * cellSize
    const totalH = GRID_H * cellSize
    ctx.fillStyle = '#0d0d0d'
    ctx.fillRect(0, 0, totalW, totalH)

    // Grid lines when zoomed in enough
    if (cellSize > 8) {
      ctx.strokeStyle = '#1a1a1a'
      ctx.lineWidth = 0.5
      const startCol = Math.max(0, Math.floor(cam.x / cellSize))
      const endCol = Math.min(GRID_W, Math.ceil((cam.x + w) / cellSize))
      const startRow = Math.max(0, Math.floor(cam.y / cellSize))
      const endRow = Math.min(GRID_H, Math.ceil((cam.y + h) / cellSize))
      for (let i = startCol; i <= endCol; i++) {
        ctx.beginPath()
        ctx.moveTo(i * cellSize, startRow * cellSize)
        ctx.lineTo(i * cellSize, endRow * cellSize)
        ctx.stroke()
      }
      for (let j = startRow; j <= endRow; j++) {
        ctx.beginPath()
        ctx.moveTo(startCol * cellSize, j * cellSize)
        ctx.lineTo(endCol * cellSize, j * cellSize)
        ctx.stroke()
      }
    }

    // Corpses (render below food and agents)
    const corpseSize = Math.max(cellSize * 0.6, 2)
    for (const c of corpses) {
      const alpha = Math.max(0.3, c.ttl / 300)
      ctx.fillStyle = `rgba(148, 163, 184, ${alpha})`
      const cx = c.x * cellSize + cellSize / 2
      const cy = c.y * cellSize + cellSize / 2
      ctx.beginPath()
      ctx.moveTo(cx, cy - corpseSize / 2)
      ctx.lineTo(cx + corpseSize / 2, cy)
      ctx.lineTo(cx, cy + corpseSize / 2)
      ctx.lineTo(cx - corpseSize / 2, cy)
      ctx.closePath()
      ctx.fill()
    }

    // Food
    const foodSize = Math.max(cellSize * 0.7, 2)
    for (const f of food) {
      const alpha = Math.max(0.4, f.ttl / 500)
      if (f.is_big) {
        const fw = (f.width || 2) * cellSize
        const fh = (f.height || 2) * cellSize
        ctx.fillStyle = `rgba(250, 204, 21, ${alpha})`
        ctx.shadowColor = 'rgba(250, 204, 21, 0.4)'
        ctx.shadowBlur = 4
        ctx.fillRect(f.x * cellSize, f.y * cellSize, fw, fh)
        ctx.strokeStyle = `rgba(250, 204, 21, ${alpha * 0.6})`
        ctx.lineWidth = 1
        ctx.strokeRect(f.x * cellSize + 1, f.y * cellSize + 1, fw - 2, fh - 2)
        ctx.shadowColor = 'transparent'
        ctx.shadowBlur = 0
      } else {
        ctx.fillStyle = `rgba(34, 197, 94, ${alpha})`
        const fx = f.x * cellSize + (cellSize - foodSize) / 2
        const fy = f.y * cellSize + (cellSize - foodSize) / 2
        ctx.fillRect(fx, fy, foodSize, foodSize)
      }
    }

    // Agents
    for (const agent of agents) {
      if (!agent.is_alive) continue
      const cx = agent.x * cellSize + cellSize / 2
      const cy = agent.y * cellSize + cellSize / 2
      const r = Math.max(cellSize * 0.4, 3)

      const hp = Math.max(0, Math.min(1, agent.existence / 100))
      const hue = hp * 120
      ctx.fillStyle = `hsl(${hue}, 70%, 50%)`

      if (agent.stress > 0.5) {
        ctx.shadowColor = 'rgba(239, 68, 68, 0.6)'
        ctx.shadowBlur = agent.stress * 6
      }

      ctx.beginPath()
      ctx.arc(cx, cy, r, 0, Math.PI * 2)
      ctx.fill()

      ctx.shadowColor = 'transparent'
      ctx.shadowBlur = 0

      // Tracked agent highlight
      if (agent.id === trackedAgent) {
        ctx.strokeStyle = '#facc15'
        ctx.lineWidth = 2
        ctx.beginPath()
        ctx.arc(cx, cy, r + 3, 0, Math.PI * 2)
        ctx.stroke()
      }

      // ID label when zoomed in
      if (cellSize > 12) {
        ctx.fillStyle = '#fff'
        ctx.font = `${Math.max(9, cellSize * 0.3)}px monospace`
        ctx.textAlign = 'center'
        ctx.textBaseline = 'middle'
        ctx.fillText(String(agent.id), cx, cy)
      }
    }

    ctx.restore()

    // HUD
    ctx.fillStyle = 'rgba(255,255,255,0.4)'
    ctx.font = '10px monospace'
    ctx.textAlign = 'left'
    ctx.textBaseline = 'top'
    const zoomPct = Math.round(cam.zoom * 100)
    const aliveCount = agents.filter(a => a.is_alive).length
    const hudText = trackedAgent !== null
      ? `${zoomPct}% | tracking #${trackedAgent} | alive ${aliveCount}/${agents.length}`
      : `${zoomPct}% | alive ${aliveCount}/${agents.length}`
    ctx.fillText(hudText, 8, 8)
  }, [agents, food, corpses, trackedAgent])

  useEffect(() => {
    rafRef.current = requestAnimationFrame(draw)
    return () => cancelAnimationFrame(rafRef.current)
  }, [draw])

  const getTooltipAt = useCallback((mx: number, my: number): TooltipInfo | null => {
    const canvas = canvasRef.current
    if (!canvas) return null
    const rect = canvas.getBoundingClientRect()
    const cam = camRef.current
    const cellSize = cam.zoom * (rect.width / GRID_W)
    const worldX = cam.x + mx
    const worldY = cam.y + my
    const gx = Math.floor(worldX / cellSize)
    const gy = Math.floor(worldY / cellSize)

    if (gx < 0 || gx >= GRID_W || gy < 0 || gy >= GRID_H) return null

    // Check agent first
    const agent = agents.find(a => a.is_alive && a.x === gx && a.y === gy)
    if (agent) {
      return {
        x: mx + 12, y: my - 10,
        text: [
          `Agent #${agent.id}`,
          `HP: ${agent.existence.toFixed(1)}  Stress: ${agent.stress.toFixed(2)}`,
          `Age: ${agent.tick_count} ticks  Action: ${agent.last_action}`,
          `Eats: ${agent.eat_count}  Attacks: ${agent.attack_count}  Signals: ${agent.signal_count}`
        ]
      }
    }

    // Check food (area-based for multi-cell BigFood)
    const foodHere = food.find(f => {
      const fw = f.width || 1
      const fh = f.height || 1
      return gx >= f.x && gx < f.x + fw && gy >= f.y && gy < f.y + fh
    })
    if (foodHere) {
      return {
        x: mx + 12, y: my - 10,
        text: [
          foodHere.is_big ? 'Big Food' : 'Food',
          `Energy: ${foodHere.energy.toFixed(1)}  TTL: ${foodHere.ttl}`
        ]
      }
    }

    // Check corpse
    const corpseHere = corpses.find(c => c.x === gx && c.y === gy)
    if (corpseHere) {
      return {
        x: mx + 12, y: my - 10,
        text: ['Corpse', `Energy: ${corpseHere.energy.toFixed(1)}  TTL: ${corpseHere.ttl}`]
      }
    }

    return null
  }, [agents, food, corpses])

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return

    const onWheel = (e: WheelEvent) => {
      e.preventDefault()
      const cam = camRef.current
      const rect = canvas.getBoundingClientRect()
      const mx = e.clientX - rect.left
      const my = e.clientY - rect.top

      const oldZoom = cam.zoom
      const factor = e.deltaY < 0 ? 1.15 : 1 / 1.15
      cam.zoom = Math.max(MIN_ZOOM, Math.min(MAX_ZOOM, oldZoom * factor))
      const ratio = cam.zoom / oldZoom

      cam.x = (cam.x + mx) * ratio - mx
      cam.y = (cam.y + my) * ratio - my

      setTrackedAgent(null)
      rafRef.current = requestAnimationFrame(draw)
    }

    const onMouseDown = (e: MouseEvent) => {
      if (e.button === 0) {
        dragRef.current = { dragging: true, lastX: e.clientX, lastY: e.clientY }
      }
    }

    const onMouseMove = (e: MouseEvent) => {
      const rect = canvas.getBoundingClientRect()
      const mx = e.clientX - rect.left
      const my = e.clientY - rect.top

      if (dragRef.current.dragging) {
        const dx = e.clientX - dragRef.current.lastX
        const dy = e.clientY - dragRef.current.lastY
        camRef.current.x -= dx
        camRef.current.y -= dy
        dragRef.current.lastX = e.clientX
        dragRef.current.lastY = e.clientY
        setTrackedAgent(null)
        rafRef.current = requestAnimationFrame(draw)
      } else {
        setTooltip(getTooltipAt(mx, my))
      }
    }

    const onMouseUp = () => { dragRef.current.dragging = false }

    const onMouseLeave = () => {
      dragRef.current.dragging = false
      setTooltip(null)
    }

    const onDblClick = (e: MouseEvent) => {
      const rect = canvas.getBoundingClientRect()
      const cam = camRef.current
      const cellSize = cam.zoom * (rect.width / GRID_W)
      const worldX = cam.x + (e.clientX - rect.left)
      const worldY = cam.y + (e.clientY - rect.top)
      const gx = Math.floor(worldX / cellSize)
      const gy = Math.floor(worldY / cellSize)

      const clicked = agents.find(a => a.is_alive && a.x === gx && a.y === gy)
      if (clicked) {
        setTrackedAgent(trackedAgent === clicked.id ? null : clicked.id)
      } else {
        setTrackedAgent(null)
      }
    }

    canvas.addEventListener('wheel', onWheel, { passive: false })
    canvas.addEventListener('mousedown', onMouseDown)
    window.addEventListener('mousemove', onMouseMove)
    window.addEventListener('mouseup', onMouseUp)
    canvas.addEventListener('mouseleave', onMouseLeave)
    canvas.addEventListener('dblclick', onDblClick)

    return () => {
      canvas.removeEventListener('wheel', onWheel)
      canvas.removeEventListener('mousedown', onMouseDown)
      window.removeEventListener('mousemove', onMouseMove)
      window.removeEventListener('mouseup', onMouseUp)
      canvas.removeEventListener('mouseleave', onMouseLeave)
      canvas.removeEventListener('dblclick', onDblClick)
    }
  }, [draw, agents, getTooltipAt, trackedAgent])

  return (
    <div className="w-full h-full relative">
      <canvas
        ref={canvasRef}
        className="w-full h-full block cursor-grab active:cursor-grabbing"
      />
      {trackedAgent !== null && (
        <button
          className="absolute top-2 right-2 text-[10px] px-2 py-0.5 rounded bg-neutral-800 text-neutral-400 hover:text-white z-10"
          onClick={() => setTrackedAgent(null)}
        >
          untrack
        </button>
      )}
      {tooltip && (
        <div
          className="absolute pointer-events-none bg-neutral-900/95 border border-neutral-700 rounded px-2 py-1.5 text-[10px] text-neutral-300 z-20 leading-relaxed"
          style={{ left: tooltip.x, top: tooltip.y }}
        >
          {tooltip.text.map((line, i) => (
            <div key={i}>{line}</div>
          ))}
        </div>
      )}
    </div>
  )
}
