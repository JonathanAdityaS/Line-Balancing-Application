import { CommonModule } from '@angular/common';
import { AfterViewInit, Component, ElementRef, inject, OnDestroy, signal, ViewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from './api.service';
import { KpiDashboardResult, KpiFilter, MasterLookupDto, StationLookupDto, TaktLogDetailDto, TaktComparisonDto } from './api.models';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

const THEME_KEY = 'srs-liba-theme';

@Component({
  selector: 'app-root',
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './app.html',
  styleUrl: './app.scss'
})
export class App implements AfterViewInit, OnDestroy {
  private readonly api = inject(ApiService);
  private readonly fb = inject(FormBuilder);

  // ---------- Data signals ----------
  protected readonly dashboard = signal<KpiDashboardResult | null>(null);
  protected readonly history = signal<TaktLogDetailDto[]>([]);
  protected readonly historyPage = signal(1);
  protected readonly historyTotalPages = signal(1);
  protected readonly historyTotalCount = signal(0);
  protected readonly pageSize = 25;

  protected readonly cells = signal<MasterLookupDto[]>([]);
  protected readonly stations = signal<StationLookupDto[]>([]);
  protected readonly meterTypes = signal<MasterLookupDto[]>([]);
  protected readonly loading = signal(false);
  protected readonly error = signal('');
protected readonly dbStatus = signal<'connected' | 'disconnected'>('disconnected');

// ---------- NEW: Takt heatmap & target config ----------
protected readonly taktHeatmap = signal<TaktComparisonDto[]>([]);
protected readonly taktTargets = signal<{ PerCell: Map<string, number>; PerStation: Map<string, number> }>({ PerCell: new Map(), PerStation: new Map() });
protected readonly loadingTakt = signal(false);

// ---------- Auth state ----------
  protected readonly isLoggedIn = signal(Boolean(localStorage.getItem('srs-liba-token')));
  protected readonly username = signal(localStorage.getItem('srs-liba-user') ?? '');
  protected readonly userRole = signal(localStorage.getItem('srs-liba-role') ?? '');
  protected readonly loginLoading = signal(false);
  protected readonly loginError = signal('');
  protected readonly loginForm = this.fb.nonNullable.group({
    username: ['', Validators.required],
    password: ['', Validators.required]
  });

  // ---------- Operator state (role "operator", dibatasi 1 cell via atribut akun) ----------
  protected readonly isOperator = signal(localStorage.getItem('srs-liba-operator') === '1');
  protected readonly assignedCellId = signal(localStorage.getItem('srs-liba-cell') ?? '');
  protected readonly assignedCellName = signal(localStorage.getItem('srs-liba-cellname') ?? '');
  protected readonly identityConfirmed = signal((localStorage.getItem('srs-liba-confirmed') ?? '1') === '1');
  protected readonly confirmLoading = signal(false);
  protected readonly confirmError = signal('');
  protected readonly confirmForm = this.fb.nonNullable.group({
    password: ['', Validators.required]
  });

  // ---------- Panel login (dashboard publik, login opsional) ----------
  protected readonly showLoginForm = signal(false);
  protected readonly showPassword = signal(false);

  // ---------- Theme (dark default) ----------
  protected readonly theme = signal<'dark' | 'light'>('dark');

  // ---------- Skeleton vs spinner ----------
  protected readonly firstLoadDone = signal(false);

  // ---------- "Diperbarui x lalu" ----------
  protected readonly lastUpdated = signal<Date | null>(null);
  protected readonly now = signal(Date.now());
  private nowTimer?: ReturnType<typeof setInterval>;

  // ---------- Sparkline history: [avg, total, waiting, util, va, unitDites] ----------
  protected readonly kpiHistory = signal<number[][]>([[], [], [], [], [], []]);
  private readonly maxHistory = 12;

  // ---------- Auto-refresh ----------
  protected readonly autoRefresh = signal(false);
  protected readonly refreshInterval = signal(30);
  private refreshTimer?: ReturnType<typeof setInterval>;

  protected readonly form = this.fb.group({
    stationId: [''],
    cell: [''],
    meterType: [''],
    dateFrom: [''],
    dateTo: ['']
  });

  @ViewChild('barChart') barChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('lineChart') lineChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('pieChart') pieChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('trendChart') trendChartRef!: ElementRef<HTMLCanvasElement>;
  @ViewChild('flowChart') flowChartRef!: ElementRef<HTMLCanvasElement>;

  private barChart?: Chart;
  private lineChart?: Chart;
  private pieChart?: Chart;
  private trendChart?: Chart;
  private flowChart?: Chart;

  ngOnInit(): void {
    // Terapkan theme tersimpan (juga diset lebih awal oleh script di index.html)
    const saved = localStorage.getItem(THEME_KEY);
    if (saved === 'light' || saved === 'dark') {
      this.theme.set(saved);
    }
    document.documentElement.setAttribute('data-theme', this.theme());

    // Ticker "diperbarui x lalu" — update tiap 5 detik
    this.nowTimer = setInterval(() => this.now.set(Date.now()), 5000);

    // Reaksi pilih Cell → refresh dropdown Station
    this.form.controls.cell.valueChanges.subscribe(cellId => {
      this.form.controls.stationId.setValue('');
      this.api.getStations(cellId ?? undefined).subscribe(data => this.stations.set(data));
    });

    // Dashboard bersifat publik: selalu muat data saat dibuka.
    // Akun yang masih login (mis. operator) diverifikasi profilnya dulu.
    if (this.isLoggedIn()) {
      this.refreshProfile();
    } else {
      this.loadMasters();
      this.load();
    }
  }

  /** Tampilkan/sembunyikan panel login (kanan atas). */
  toggleLoginForm(): void {
    this.showLoginForm.update(v => !v);
    this.loginError.set('');
  }

  /** Tampilkan/sembunyikan teks password di form login. */
  togglePassword(): void {
    this.showPassword.update(v => !v);
  }

  /** Isi otomatis kredensial demo (admin/admin atau operator/operator). */
  fillDemo(role: 'admin' | 'operator'): void {
    this.loginForm.setValue({ username: role, password: role });
    this.loginError.set('');
  }

  /** True bila operator yang belum mengonfirmasi identitas → dashboard digate. */
  get needsIdentityConfirm(): boolean {
    return this.isOperator() && !this.identityConfirmed();
  }

  /** Ambil profil terbaru dari server (status konfirmasi & cell operator). */
  private refreshProfile(): void {
    this.api.getMe().subscribe({
      next: (me) => {
        this.applyProfile(me.isOperator, me.assignedCellId, me.assignedCellName, me.identityConfirmed);
        if (this.isOperator()) this.applyOperatorLock();
        if (this.needsIdentityConfirm) return; // tunggu konfirmasi identitas
        this.loadMasters();
        this.load();
      },
      error: () => {
        // Fallback: token mungkin basi — coba muat seperti biasa
        if (this.isOperator()) this.applyOperatorLock();
        this.loadMasters();
        this.load();
      }
    });
  }

  /** Simpan profil operator ke signal + localStorage. */
  private applyProfile(isOperator: boolean, assignedCellId: number | null, assignedCellName: string | null, identityConfirmed: boolean): void {
    this.isOperator.set(isOperator);
    this.assignedCellId.set(assignedCellId?.toString() ?? '');
    this.assignedCellName.set(assignedCellName ?? '');
    this.identityConfirmed.set(identityConfirmed);
    localStorage.setItem('srs-liba-operator', isOperator ? '1' : '0');
    localStorage.setItem('srs-liba-cell', assignedCellId?.toString() ?? '');
    localStorage.setItem('srs-liba-cellname', assignedCellName ?? '');
    localStorage.setItem('srs-liba-confirmed', identityConfirmed ? '1' : '0');
  }

  /** Kunci filter cell ke cell operator (dropdown disabled, nilai tetap terkirim). */
  private applyOperatorLock(): void {
    const cell = this.assignedCellId();
    if (cell) this.form.controls.cell.setValue(cell);
    this.form.controls.cell.disable();
  }

  ngAfterViewInit(): void {}

  ngOnDestroy(): void {
    if (this.refreshTimer) clearInterval(this.refreshTimer);
    if (this.nowTimer) clearInterval(this.nowTimer);
  }

  // ============================================================
  // THEME
  // ============================================================

  /** Ganti dark/light, persist ke localStorage, chart di-render ulang. */
  toggleTheme(): void {
    const next = this.theme() === 'dark' ? 'light' : 'dark';
    this.theme.set(next);
    document.documentElement.setAttribute('data-theme', next);
    localStorage.setItem(THEME_KEY, next);

    const d = this.dashboard();
    if (d) setTimeout(() => this.renderCharts(d));
  }

  /** Palet warna chart mengikuti theme aktif. */
  private palette() {
    const dark = this.theme() === 'dark';
    return {
      text: dark ? '#e2e8f0' : '#0f172a',
      muted: dark ? '#94a3b8' : '#64748b',
      grid: dark ? '#2b3a52' : '#e2e8f0',
      panel: dark ? '#1e293b' : '#ffffff',
      accent: dark ? '#3b82f6' : '#2563eb'
    };
  }

  // ============================================================
  // SPARKLINE & TIMESTAMP
  // ============================================================

  /** Simpan riwayat 5 KPI (maks 12 titik) untuk sparkline kartu. */
  private pushKpi(...values: number[]): void {
    this.kpiHistory.update(hist =>
      hist.map((arr, i) => [...arr, values[i]].slice(-this.maxHistory))
    );
  }

  /** Konversi deret angka → points polyline SVG (viewBox 100x30). */
  protected sparklinePoints(idx: number): string {
    const values = this.kpiHistory()[idx] ?? [];
    if (values.length < 2) return '0,28 100,28';
    const min = Math.min(...values);
    const max = Math.max(...values);
    const range = max - min || 1;
    return values
      .map((v, i) => `${((i * 100) / (values.length - 1)).toFixed(1)},${(28 - ((v - min) / range) * 26).toFixed(1)}`)
      .join(' ');
  }

  /** Lebar bar waiting (maks 100%) — skala 300 detik = penuh. */
  protected waitBar(seconds: number): number {
    return Math.min(100, Math.round(seconds / 3));
  }

  /**
   * Agregat takt status seluruh line (worst-case wins):
   * ada overload → OVERLOAD; else ada warning → WARNING; else NORMAL.
   * Disertai distribusi jumlah station per kategori untuk sub-label kartu.
   */
  protected taktAggregate(): { status: string; overload: number; warning: number; normal: number } {
    const takt = this.dashboard()?.taktComparison ?? [];
    const overload = takt.filter(x => x.status === 'overload').length;
    const warning = takt.filter(x => x.status === 'warning').length;
    const normal = takt.filter(x => x.status === 'normal').length;
    const status = overload > 0 ? 'overload' : warning > 0 ? 'warning' : 'normal';
    return { status, overload, warning, normal };
  }

  /**
   * Kelompokkan data takt heatmap per cell untuk grid Cell × Station.
   * Urutan cell & station mengikuti urutan datang dari API (sort numerik).
   */
  get heatmapRows(): { cell: string; stations: TaktComparisonDto[] }[] {
    const map = new Map<string, TaktComparisonDto[]>();
    for (const s of this.taktHeatmap()) {
      const arr = map.get(s.cellName) ?? [];
      arr.push(s);
      map.set(s.cellName, arr);
    }
    return [...map.entries()].map(([cell, stations]) => ({ cell, stations }));
  }

  /** Kelas warna tile heatmap berdasar status takt (abu-abu bila tanpa data). */
  heatTileClass(s: TaktComparisonDto): string {
    if (s.averageCycleTimeSeconds <= 0) return 'heat-tile heat-nodata';
    if (s.status === 'overload') return 'heat-tile heat-overload';
    if (s.status === 'warning') return 'heat-tile heat-warning';
    return 'heat-tile heat-normal';
  }

  /** Tooltip tile heatmap: nama, cell, rata-rata, dan status. */
  heatTitle(s: TaktComparisonDto): string {
    return `${s.stationName} (${s.cellName}) — avg ${s.averageCycleTimeSeconds.toFixed(1)}s, status ${s.status}`;
  }

  /** Teks "x lalu" untuk timestamp update terakhir. */
  get updatedAgoText(): string {
    const lu = this.lastUpdated();
    if (!lu) return '';
    const s = Math.max(0, Math.round((this.now() - lu.getTime()) / 1000));
    if (s < 60) return `${s} detik lalu`;
    const m = Math.floor(s / 60);
    return `${m} menit ${s % 60} detik lalu`;
  }

  // ============================================================
  // MASTER DATA & LOAD
  // ============================================================

  loadMasters(): void {
    this.api.getCells().subscribe({ next: data => this.cells.set(data) });
    this.api.getStations().subscribe({ next: data => this.stations.set(data) });
    this.api.getMeterTypes().subscribe({ next: data => this.meterTypes.set(data) });
  }

  load(): void {
    this.loading.set(true);
    this.error.set('');
    const filter = this.toFilter();

    // Validasi rentang tanggal di sisi klien agar tidak perlu request sia-sia
    if (filter.dateFrom && filter.dateTo && filter.dateFrom > filter.dateTo) {
      this.error.set('DateFrom tidak boleh lebih besar dari DateTo.');
      this.loading.set(false);
      return;
    }

    this.api.getDashboard(filter).subscribe({
      next: data => {
        this.dashboard.set(data);
        this.dbStatus.set('connected');
        this.lastUpdated.set(new Date());
        this.firstLoadDone.set(true);

        // Simpan riwayat KPI untuk sparkline (termasuk unit unik yang sudah dites)
        const s = data.summary;
        this.pushKpi(s.averagePerStation, s.totalTest, s.overallWaitingAvgSeconds, s.overallUtilizationPercent, s.overallVaPercent, s.totalUniqueUnits);

        setTimeout(() => this.renderCharts(data), 50);
      },
      error: () => {
        this.error.set('API gagal connect');
        this.dbStatus.set('disconnected');
        this.loading.set(false);
      },
      complete: () => this.loading.set(false)
    });

    this.loadHistory(filter, 1);

    // Fetch takt heatmap and targets after dashboard load
    this.fetchTaktHeatmap(filter);
    this.fetchTaktTargets();
  }

  loadHistory(filter: KpiFilter, page: number): void {
    this.api.getHistory(filter, page, this.pageSize).subscribe({
      next: data => {
        this.history.set(data.items);
        this.historyPage.set(data.page);
        this.historyTotalPages.set(data.totalPages);
        this.historyTotalCount.set(data.totalCount);
      },
      error: () => this.error.set('Gagal memuat history')
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.historyTotalPages()) return;
    this.loadHistory(this.toFilter(), page);
  }

  /** Drill-down: klik baris station di Ringkasan Akhir → filter station itu. */
  selectStation(stationId: string): void {
    this.form.controls.stationId.setValue(stationId);
    this.load();
  }

  /** Reset semua filter lalu muat ulang data (cell operator tetap terkunci). */
  clearFilters(): void {
    if (this.isOperator()) {
      this.form.patchValue({ stationId: '', meterType: '', dateFrom: '', dateTo: '' });
      this.applyOperatorLock();
    } else {
      this.form.patchValue({ stationId: '', cell: '', meterType: '', dateFrom: '', dateTo: '' });
    }
    this.load();
    this.fetchTaktHeatmap();
    this.fetchTaktTargets();
  }

  /** Cek apakah ada filter aktif (untuk tombol reset). */
  get hasActiveFilter(): boolean {
    const raw = this.form.getRawValue();
    return Boolean(raw.cell || raw.stationId || raw.meterType || raw.dateFrom || raw.dateTo);
  }

  /** Fetch heatmap Takt per cell/station. */
  fetchTaktHeatmap(filter?: KpiFilter): void {
    this.loadingTakt.set(true);
    this.api.getTaktHeatmap(filter ?? this.toFilter()).subscribe({
      next: data => this.taktHeatmap.set(data),
      error: () => this.error.set('Gagal memuat heatmap takt'),
      complete: () => this.loadingTakt.set(false)
    });
  }

  /** Ambil konfigurasi target takt per cell/station. */
  fetchTaktTargets(): void {
    this.loadingTakt.set(true);
    this.api.getTaktTargets().subscribe({
      next: data => this.taktTargets.set(data),
      error: () => this.error.set('Gagal memuat target takt'),
      complete: () => this.loadingTakt.set(false)
    });
  }

  // ============================================================
  // AUTH
  // ============================================================

  login(): void {
    if (this.loginForm.invalid) return;
    this.loginLoading.set(true);
    this.loginError.set('');
    const { username, password } = this.loginForm.getRawValue();
    this.api.login(username, password).subscribe({
      next: (res) => {
        localStorage.setItem('srs-liba-token', res.token);
        localStorage.setItem('srs-liba-role', res.role);
        localStorage.setItem('srs-liba-user', res.username);
        this.username.set(res.username);
        this.userRole.set(res.role);
        this.applyProfile(res.isOperator, res.assignedCellId, res.assignedCellName, res.identityConfirmed);
        this.isLoggedIn.set(true);
        this.loginLoading.set(false);
        this.showLoginForm.set(false);
        if (this.isOperator()) this.applyOperatorLock();
        if (this.needsIdentityConfirm) return; // operator wajib konfirmasi dulu
        this.loadMasters();
        this.load();
      },
      error: (err) => {
        this.loginError.set(err.status === 401 ? 'Username atau password salah' : 'Gagal login ke server');
        this.loginLoading.set(false);
      }
    });
  }

  /** Konfirmasi identitas operator (verifikasi ulang password). */
  confirmIdentity(): void {
    if (this.confirmForm.invalid) return;
    this.confirmLoading.set(true);
    this.confirmError.set('');
    const { password } = this.confirmForm.getRawValue();
    this.api.confirmIdentity(password).subscribe({
      next: (res) => {
        localStorage.setItem('srs-liba-token', res.token);
        this.applyProfile(res.isOperator, res.assignedCellId, res.assignedCellName, res.identityConfirmed);
        this.confirmLoading.set(false);
        this.confirmForm.reset();
        this.loadMasters();
        this.load();
      },
      error: (err) => {
        this.confirmError.set(err.status === 401 ? 'Password salah. Konfirmasi identitas gagal.' : 'Gagal mengonfirmasi identitas');
        this.confirmLoading.set(false);
      }
    });
  }

  logout(): void {
    localStorage.removeItem('srs-liba-token');
    localStorage.removeItem('srs-liba-role');
    localStorage.removeItem('srs-liba-user');
    localStorage.removeItem('srs-liba-operator');
    localStorage.removeItem('srs-liba-cell');
    localStorage.removeItem('srs-liba-cellname');
    localStorage.removeItem('srs-liba-confirmed');
    this.isLoggedIn.set(false);
    this.username.set('');
    this.userRole.set('');
    this.isOperator.set(false);
    this.assignedCellId.set('');
    this.assignedCellName.set('');
    this.identityConfirmed.set(true);
    this.confirmForm.reset();
    this.confirmError.set('');
    this.form.controls.cell.enable();
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = undefined;
      this.autoRefresh.set(false);
    }
    // Dashboard publik: tetap tampil dengan data tanpa filter akun
    this.form.patchValue({ stationId: '', cell: '', meterType: '', dateFrom: '', dateTo: '' });
    this.loadMasters();
    this.load();
  }

  // ============================================================
  // AUTO-REFRESH
  // ============================================================

  toggleAutoRefresh(): void {
    this.autoRefresh.update(v => !v);
    this.applyAutoRefresh();
  }

  onIntervalChange(event: Event): void {
    const value = Number((event.target as HTMLSelectElement).value);
    this.refreshInterval.set(value);
    if (this.autoRefresh()) this.applyAutoRefresh();
  }

  private applyAutoRefresh(): void {
    if (this.refreshTimer) {
      clearInterval(this.refreshTimer);
      this.refreshTimer = undefined;
    }
    if (this.autoRefresh()) {
      this.refreshTimer = setInterval(() => this.load(), this.refreshInterval() * 1000);
    }
  }

  // ============================================================
  // EXPORT
  // ============================================================

  exportExcel(): void {
    this.api.exportExcel(this.toFilter()).subscribe({
      next: (blob) => this.downloadBlob(blob, 'line-balancing-dashboard.xlsx'),
      error: () => this.error.set('Gagal export Excel')
    });
  }

  exportPdf(): void {
    this.api.exportPdf(this.toFilter()).subscribe({
      next: (blob) => this.downloadBlob(blob, 'line-balancing-dashboard.pdf'),
      error: () => this.error.set('Gagal export PDF')
    });
  }

  private downloadBlob(blob: Blob, filename: string): void {
    if (typeof window === 'undefined') return;
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = filename;
    a.click();
    URL.revokeObjectURL(url);
  }

  // ============================================================
  // CHARTS
  // ============================================================

  private renderCharts(data: KpiDashboardResult): void {
    this.renderBarChart(data);
    this.renderLineChart(data);
    this.renderPieChart(data);
    this.renderTrendChart(data);
    this.renderFlowChart(data);
  }

  private baseScales() {
    const p = this.palette();
    return {
      x: { ticks: { color: p.muted }, grid: { color: 'transparent' } },
      y: { ticks: { color: p.muted }, grid: { color: p.grid } }
    };
  }

  private renderBarChart(data: KpiDashboardResult): void {
    this.barChart?.destroy();
    const canvas = this.barChartRef?.nativeElement;
    if (!canvas) return;
    const p = this.palette();
    this.barChart = new Chart(canvas, {
      type: 'bar',
      data: {
        labels: data.averagePerStation.map(x => x.stationName),
        datasets: [{
          label: 'Avg Cycle Time (s)',
          data: data.averagePerStation.map(x => x.averageCycleTimeSeconds),
          backgroundColor: p.accent,
          borderRadius: 6,
          maxBarThickness: 42
        }]
      },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: this.baseScales()
      }
    });
  }

  private renderLineChart(data: KpiDashboardResult): void {
    this.lineChart?.destroy();
    const canvas = this.lineChartRef?.nativeElement;
    if (!canvas) return;
    const p = this.palette();
    this.lineChart = new Chart(canvas, {
      type: 'line',
      data: {
        labels: data.averagePerStation.map(x => x.stationName),
        datasets: [{
          label: 'Total Test',
          data: data.averagePerStation.map(x => x.totalTest),
          borderColor: p.accent,
          // Gradient fill lembut dari atas ke bawah area chart
          backgroundColor: (context: any) => {
            const area = context.chart.chartArea;
            if (!area) return 'transparent';
            const g = context.chart.ctx.createLinearGradient(0, area.top, 0, area.bottom);
            g.addColorStop(0, this.theme() === 'dark' ? 'rgba(59,130,246,0.35)' : 'rgba(37,99,235,0.25)');
            g.addColorStop(1, 'rgba(59,130,246,0.02)');
            return g;
          },
          fill: true,
          tension: 0.35,
          pointRadius: 3,
          pointHoverRadius: 5,
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: this.baseScales()
      }
    });
  }

  // Plugin kecil: teks VA% besar di tengah donut
  private readonly centerTextPlugin = {
    id: 'centerText',
    afterDraw: (chart: Chart) => {
      const type = (chart.config as { type?: string }).type;
      if (type !== 'doughnut') return;
      const meta = chart.getDatasetMeta(0);
      if (!meta.data.length) return;
      const el = meta.data[0] as unknown as { x: number; y: number };
      const ctx = chart.ctx;
      const p = this.palette();
      ctx.save();
      ctx.textAlign = 'center';
      ctx.textBaseline = 'middle';
      ctx.font = '700 22px Inter, sans-serif';
      ctx.fillStyle = p.text;
      ctx.fillText(`${this.dashboard()?.summary.overallVaPercent ?? 0}%`, el.x, el.y - 6);
      ctx.font = '600 9px Inter, sans-serif';
      ctx.fillStyle = p.muted;
      ctx.fillText('VALUE ADD', el.x, el.y + 14);
      ctx.restore();
    }
  };

  private renderPieChart(data: KpiDashboardResult): void {
    this.pieChart?.destroy();
    const canvas = this.pieChartRef?.nativeElement;
    if (!canvas) return;
    const p = this.palette();
    const totalVa = data.vaNva.reduce((s, x) => s + x.vaTimeSeconds, 0);
    const totalNva = data.vaNva.reduce((s, x) => s + x.nvaTimeSeconds, 0);
    this.pieChart = new Chart<'doughnut', number[], string>(canvas, {
      type: 'doughnut',
      data: {
        labels: ['Value-Add', 'Non Value-Add'],
        datasets: [{
          data: [totalVa, totalNva],
          backgroundColor: ['#6366f1', '#f43f5e'],
          borderColor: p.panel,
          borderWidth: 3,
          hoverOffset: 6
        }]
      },
      options: {
        responsive: true,
        cutout: '68%',
        plugins: { legend: { labels: { color: p.muted, usePointStyle: true } } }
      },
      plugins: [this.centerTextPlugin]
    });
  }

  private renderTrendChart(data: KpiDashboardResult): void {
    this.trendChart?.destroy();
    const canvas = this.trendChartRef?.nativeElement;
    if (!canvas) return;
    const p = this.palette();
    this.trendChart = new Chart(canvas, {
      type: 'line',
      data: {
        labels: data.utilization.map(x => x.stationName),
        datasets: [
          {
            label: 'Utilization %',
            data: data.utilization.map(x => x.utilizationPercent),
            borderColor: '#f59e0b',
            backgroundColor: 'rgba(245,158,11,0.08)',
            fill: true,
            tension: 0.35,
            pointRadius: 2,
            borderWidth: 2
          },
          {
            label: 'Waiting Time (s)',
            data: data.waitingTime.map(x => x.averageWaitingTimeSeconds),
            borderColor: '#ef4444',
            backgroundColor: 'transparent',
            tension: 0.35,
            pointRadius: 2,
            borderWidth: 2
          }
        ]
      },
      options: {
        responsive: true,
        plugins: { legend: { labels: { color: p.muted, usePointStyle: true } } },
        scales: this.baseScales()
      }
    });
  }

  private renderFlowChart(data: KpiDashboardResult): void {
    this.flowChart?.destroy();
    const canvas = this.flowChartRef?.nativeElement;
    if (!canvas) return;
    const p = this.palette();
    const totals = [
      data.unitFlow.reduce((s, x) => s + x.unitsWith1Test, 0),
      data.unitFlow.reduce((s, x) => s + x.unitsWith2Tests, 0),
      data.unitFlow.reduce((s, x) => s + x.unitsWith3Tests, 0),
      data.unitFlow.reduce((s, x) => s + x.unitsWith4Tests, 0),
      data.unitFlow.reduce((s, x) => s + x.unitsWith5Tests, 0)
    ];
    this.flowChart = new Chart(canvas, {
      type: 'bar',
      data: {
        labels: ['1 Test', '2 Test', '3 Test', '4 Test', '5 Test (Lulus)'],
        datasets: [{
          label: 'Jumlah Unit',
          data: totals,
          backgroundColor: ['#ef4444', '#f97316', '#eab308', '#84cc16', '#22c55e'],
          borderRadius: 6,
          maxBarThickness: 46
        }]
      },
      options: {
        responsive: true,
        plugins: { legend: { display: false } },
        scales: {
          x: { ticks: { color: p.muted }, grid: { color: 'transparent' } },
          y: { ticks: { color: p.muted, stepSize: 1 }, grid: { color: p.grid } }
        }
      }
    });
  }

  // ============================================================
  // FILTER HELPER
  // ============================================================

  private toFilter(): KpiFilter {
    const raw = this.form.getRawValue();
    return {
      cellId: raw.cell ?? undefined,
      stationId: raw.stationId ?? undefined,
      meterTypeId: raw.meterType ?? undefined,
      dateFrom: raw.dateFrom || undefined,
      dateTo: raw.dateTo || undefined
    };
  }
}
