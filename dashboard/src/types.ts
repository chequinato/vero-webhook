export interface TransacaoDto {
  id: string;
  status: string;
  valor: number;
  tipo: string;
  moeda: string;
  motivo: string | null;
  riskScore: number | null;
  timestamp: string;
  remetenteId: number;
  destinatarioId: number;
  createdAt: string;
}

export interface Stats {
  total: number;
  aprovadas: number;
  bloqueadas: number;
  suspeitas: number;
  aceitasProvisoria: number;
  valorTotal: number;
  valorMedio: number;
  riskScoreMedio: number;
}

export interface VolumeHora {
  hora: string;
  quantidade: number;
  valor: number;
  bloqueadas: number;
  suspeitas: number;
}

export interface PaginatedResponse {
  items: TransacaoDto[];
  total: number;
  page: number;
  size: number;
  totalPages: number;
}

export interface MlMetrics {
  modelLoaded: boolean;
  lastTrained: string | null;
  algorithm: string;
  features: string[];
}

/** Resposta de POST /api/dashboard/transactions/{id}/reavaliar. */
export interface Reavaliacao {
  id: string;
  status: string;
  motivo: string | null;
  riskScore: number;
  limiar: number;
}

/** Payload de `StatusAtualizado` vindo do hub. */
export interface StatusPatch {
  id: string;
  status: string;
  motivo: string | null;
  riskScore: number | null;
}

export interface AlertItem {
  id: string;
  transacaoId: string;
  motivo: string;
  type: 'danger' | 'warning' | 'success';
  message: string;
  timestamp: Date;
}
