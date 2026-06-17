import { useRef, useEffect } from 'react'
import type { AgentSnapshot, FoodTile } from '../types'

const GRID_W = 20
const GRID_H = 20

interface Props {
  agents: AgentSnapshot[]
  food: FoodTile[]
}

export function WorldMap({ agents, food }: Props) {
  const canvasRef = useRef<HTMLCanvasElement>(null)

  useEffect(() => {
    const canvas = canvasRef.current
    if (!canvas) return
    const ctx = canvas.getContext('2d')
    if (!ctx) return

    const rect = canvas.getBoundingClientRect()
    const size = Math.min(rect.width, rect.height)
    canvas.width = size
    canvas.height = size
    const cellSize = size / GRID_W

    // Background
    ctx.fillStyle = '#0d0d0d'
    ctx.fillRect(0, 0, size, size)

    // Grid lines
    ctx.strokeStyle = '#1a1a1a'
    ctx.lineWidth = 0.5
    for (let i = 0; i <= GRID_W; i++) {
      ctx.beginPath()
      ctx.moveTo(i * cellSize, 0)
      ctx.lineTo(i * cellSize, size)
      ctx.stroke()
    }
    for (let j = 0; j <= GRID_H; j++) {
      ctx.beginPath()
      ctx.moveTo(0, j * cellSize)
      ctx.lineTo(size, j * cellSize)
      ctx.stroke()
    }

    // Food tiles
    for (const f of food) {
      const alpha = Math.max(0.3, f.ttl / 100)
      ctx.fillStyle = `rgba(34, 197, 94, ${alpha})`
      ctx.fillRect(
        f.x * cellSize + 2,
        f.y * cellSize + 2,
        cellSize - 4,
        cellSize - 4
      )
    }

    // Agents
    for (const agent of agents) {
      if (!agent.is_alive) continue
      const cx = agent.x * cellSize + cellSize / 2
      const cy = agent.y * cellSize + cellSize / 2
      const r = cellSize * 0.35

      // Color by existence (green→yellow→red)
      const hp = Math.max(0, Math.min(1, agent.existence / 100))
      const hue = hp * 120 // 0=red, 120=green
      ctx.fillStyle = `hsl(${hue}, 70%, 50%)`

      // Stress glow
      if (agent.stress > 0.5) {
        ctx.shadowColor = 'rgba(239, 68, 68, 0.6)'
        ctx.shadowBlur = agent.stress * 4
      }

      ctx.beginPath()
      ctx.arc(cx, cy, r, 0, Math.PI * 2)
      ctx.fill()

      ctx.shadowColor = 'transparent'
      ctx.shadowBlur = 0

      // Agent ID label
      ctx.fillStyle = '#fff'
      ctx.font = `${Math.max(8, cellSize * 0.3)}px monospace`
      ctx.textAlign = 'center'
      ctx.textBaseline = 'middle'
      ctx.fillText(String(agent.id), cx, cy)
    }
  }, [agents, food])

  return (
    <div className="w-full h-full flex items-center justify-center p-2">
      <canvas
        ref={canvasRef}
        className="w-full h-full"
        style={{ aspectRatio: '1 / 1', maxWidth: '100%', maxHeight: '100%' }}
      />
    </div>
  )
}
