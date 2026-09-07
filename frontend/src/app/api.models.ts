export interface MasterLookupDto {
  id: number;
  name: string;
}

export interface StationLookupDto {
  id: number;
  name: string;
  cellId: number;
  cellName: string;
}

export interface KpiFilter {
  cellId?: string;
  stationId?: string;
  meterTypeId?: string;
  dateFrom?: string;
  dateTo?: string;
  serialNumber?: string;
}

export interface OperatorProfile {
  username: string;
  role: string;
  isOperator: boolean;
  assignedCellId: number | null;
  assignedCellName: string | null;
  identityConfirmed: boolean;
}

export interface LoginResponse extends OperatorProfile {
  token: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface StationAverageDto {
  stationId: string;
  stationName: string;
  cellName: string;
  averageCycleTimeSeconds: number;
  totalTest: number;
}

export interface WaitingTimeDto {
  stationId: string;
  stationName: string;
  averageWaitingTimeSeconds: number;
  totalWaitingTimeSeconds: number;
}

export interface UtilizationDto {
  stationId: string;
  stationName: string;
  activeTimeSeconds: number;
  availableTimeSeconds: number;
  utilizationPercent: number;
}

export interface VaNvaDto {
  stationId: string;
  stationName: string;
  vaTimeSeconds: number;
  nvaTimeSeconds: number;
  vaPercent: number;
}

export interface TaktComparisonDto {
  stationId: string;
  stationName: string;
  cellName: string;
  averageCycleTimeSeconds: number;
  status: string;
}

export interface CellSummaryDto {
  cellName: string;
  totalTest: number;
  avgDurationSeconds: number;
  avgWaitingTimeSeconds: number;
  utilizationPercent: number;
  totalVaSeconds: number;
  totalNvaSeconds: number;
}

export interface UnitFlowDto {
  cellName: string;
  registeredUnits: number;
  totalUniqueUnits: number;
  completedAllStations: number;
  completionRatePercent: number;
  unitsWith1Test: number;
  unitsWith2Tests: number;
  unitsWith3Tests: number;
  unitsWith4Tests: number;
  unitsWith5Tests: number;
}

export interface UnitFlowDetailDto {
  serialNumber: string;
  cellName: string;
  testCount: number;
  stationsPassed: string;
}

export interface StationSummaryDto {
  stationId: string;
  stationName: string;
  cellName: string;
  avgCycleTimeSeconds: number;
  totalTest: number;
  avgWaitingTimeSeconds: number;
  utilizationPercent: number;
  vaTimeSeconds: number;
  nvaTimeSeconds: number;
  vaPercent: number;
  taktStatus: string;
  hasData: boolean;
}

export interface KpiDashboardResult {
  summary: {
    averagePerStation: number;
    totalTest: number;
    totalUniqueUnits: number;
    totalRegisteredUnits: number;
    untestedUnits: number;
    overallWaitingAvgSeconds: number;
    overallUtilizationPercent: number;
    overallVaPercent: number;
  };
  averagePerStation: StationAverageDto[];
  waitingTime: WaitingTimeDto[];
  utilization: UtilizationDto[];
  vaNva: VaNvaDto[];
  taktComparison: TaktComparisonDto[];
  cellSummary: CellSummaryDto[];
  unitFlow: UnitFlowDto[];
  unitFlowDetail: UnitFlowDetailDto[];
  stationSummary: StationSummaryDto[];
}

export interface TaktLogDetailDto {
  id: number;
  stationName: string;
  cellName: string;
  meterTypeName: string;
  serialNumber: string;
  arrivalTime: string;
  startTime: string;
  endTime: string;
  durationSeconds: number;
}

/// <summary>
/// Konfigurasi target takt per cell/station.
/// Digunakan oleh endpoint GET/POST /api/kpi/takt-targets.
/// </summary>
export interface KpiOptions {
  TargetTaktSeconds: number;
  TaktTargets: TaktTargetConfig;
}

/// <summary>
/// Mapping target takt per cell/station.
/// Key: "CellName|StationName" atau "CellName" untuk default cell-wide.
/// </summary>
export interface TaktTargetConfig {
  PerCell: Map<string, number>;
  PerStation: Map<string, number>;
}

