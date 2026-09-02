import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { KpiDashboardResult, KpiFilter, MasterLookupDto, PagedResult, StationLookupDto, TaktLogDetailDto, TaktComparisonDto } from './api.models';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = 'http://127.0.0.1:5121';

  getCells(): Observable<MasterLookupDto[]> {
    return this.http.get<MasterLookupDto[]>(`${this.baseUrl}/api/master/cells`);
  }

  getStations(cellId?: string): Observable<StationLookupDto[]> {
    let params = new HttpParams();
    if (cellId) params = params.set('cellId', cellId);
    return this.http.get<StationLookupDto[]>(`${this.baseUrl}/api/master/stations`, { params });
  }

  getMeterTypes(): Observable<MasterLookupDto[]> {
    return this.http.get<MasterLookupDto[]>(`${this.baseUrl}/api/master/meter-types`);
  }

  login(username: string, password: string): Observable<{ token: string, username: string, role: string }> {
    return this.http.post<{ token: string, username: string, role: string }>(`${this.baseUrl}/api/auth/login`, { username, password });
  }

  getDashboard(filter: KpiFilter): Observable<KpiDashboardResult> {
    return this.http.get<KpiDashboardResult>(`${this.baseUrl}/api/kpi/dashboard`, { params: this.toParams(filter) });
  }

  getHistory(filter: KpiFilter, page: number, pageSize: number): Observable<PagedResult<TaktLogDetailDto>> {
    let params = this.toParams(filter).set('page', page.toString()).set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<TaktLogDetailDto>>(`${this.baseUrl}/api/kpi/history`, { params });
  }

  exportExcel(filter: KpiFilter): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/api/export/excel`, {
      params: this.toParams(filter),
      responseType: 'blob' as const
    });
  }

  exportPdf(filter: KpiFilter): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/api/export/pdf`, {
      params: this.toParams(filter),
      responseType: 'blob' as const
    });
  }

  getTaktHeatmap(filter: KpiFilter): Observable<TaktComparisonDto[]> {
    return this.http.get<TaktComparisonDto[]>(`${this.baseUrl}/api/kpi/heatmap/takt`, { params: this.toParams(filter) });
  }

  getTaktTargets(): Observable<{ PerCell: Map<string, number>; PerStation: Map<string, number> }> {
    return this.http.get<{ PerCell: Map<string, number>; PerStation: Map<string, number> }>(`${this.baseUrl}/api/kpi/takt-targets`);
  }

  updateTaktTargets(config: { PerCell: Map<string, number>; PerStation: Map<string, number> }): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/api/kpi/takt-targets`, config);
  }

  private toParams(filter: KpiFilter): HttpParams {
    let params = new HttpParams();
    Object.entries(filter).forEach(([key, value]) => {
      if (value) params = params.set(key, value);
    });
    return params;
  }

  private toQuery(filter: KpiFilter): string {
    return this.toParams(filter).toString();
  }
}