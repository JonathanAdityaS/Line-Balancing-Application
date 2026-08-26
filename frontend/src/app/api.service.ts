import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { KpiDashboardResult, KpiFilter, MasterLookupDto, PagedResult, StationLookupDto, TaktLogDetailDto } from './api.models';

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

  getDashboard(filter: KpiFilter): Observable<KpiDashboardResult> {
    return this.http.get<KpiDashboardResult>(`${this.baseUrl}/api/kpi/dashboard`, { params: this.toParams(filter) });
  }

  getHistory(filter: KpiFilter, page: number, pageSize: number): Observable<PagedResult<TaktLogDetailDto>> {
    let params = this.toParams(filter).set('page', page.toString()).set('pageSize', pageSize.toString());
    return this.http.get<PagedResult<TaktLogDetailDto>>(`${this.baseUrl}/api/kpi/history`, { params });
  }

  exportExcel(filter: KpiFilter): string {
    return `${this.baseUrl}/api/export/excel?${this.toQuery(filter)}`;
  }

  exportPdf(filter: KpiFilter): string {
    return `${this.baseUrl}/api/export/pdf?${this.toQuery(filter)}`;
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
