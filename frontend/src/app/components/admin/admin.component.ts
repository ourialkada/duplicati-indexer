import { Component, inject, OnInit, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  ShipAlert,
  ShipButton,
  ShipCard,
  ShipDivider,
  ShipFormField,
  ShipIcon,
  ShipList,
  ShipSidenav,
  ShipSpinner,
  ShipDialogService,
} from '@ship-ui/core';
import { ConfirmDialogComponent } from '../confirm-dialog/confirm-dialog.component';
import LogoComponent from '../logo/logo.component';
import { AuthService } from '../../services/auth.service';
import {
  BackupSource,
  BackupSourceService,
  CreateBackupSourceRequest,
} from '../../services/backup-source.service';

@Component({
  selector: 'app-admin',
  imports: [
    CommonModule,
    FormsModule,
    RouterLink,
    ShipAlert,
    ShipButton,
    ShipCard,
    ShipDivider,
    ShipFormField,
    ShipIcon,
    ShipList,
    ShipSidenav,
    ShipSpinner,
    LogoComponent,
  ],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss',
})
export default class AdminComponent implements OnInit {
  private backupSourceService = inject(BackupSourceService);
  authService = inject(AuthService);
  private dialog = inject(ShipDialogService);

  sources = signal<BackupSource[]>([]);
  isLoading = signal(false);
  error = signal('');
  success = signal('');
  isNavOpen = signal(true);
  isDarkMode = signal(false);

  // Add form
  showAddForm = signal(false);
  formName = signal('');
  formBackupId = signal('');
  formTargetUrl = signal('');
  formPassword = signal('');
  isSubmitting = signal(false);

  // Index trigger
  indexSourceId = signal<string | null>(null);
  indexDlistFilename = signal('');
  isIndexing = signal(false);

  toggleBodyClass(): void {
    this.isDarkMode.set(!this.isDarkMode());
    if (this.isDarkMode()) {
      document.documentElement.classList.add('dark');
      document.documentElement.classList.remove('light');
    } else {
      document.documentElement.classList.remove('dark');
      document.documentElement.classList.add('light');
    }
  }

  ngOnInit(): void {
    this.loadSources();
  }

  loadSources(): void {
    this.isLoading.set(true);
    this.backupSourceService.getAll().subscribe({
      next: (data) => {
        this.sources.set(data);
        this.isLoading.set(false);
      },
      error: (err) => {
        console.error('Failed to load backup sources', err);
        this.error.set('Failed to load backup sources');
        this.isLoading.set(false);
      },
    });
  }

  addSource(): void {
    const request: CreateBackupSourceRequest = {
      name: this.formName().trim(),
      duplicatiBackupId: this.formBackupId().trim(),
      targetUrl: this.formTargetUrl().trim(),
      encryptionPassword: this.formPassword().trim() || undefined,
    };

    if (!request.name || !request.duplicatiBackupId) return;

    this.isSubmitting.set(true);
    this.error.set('');

    this.backupSourceService.create(request).subscribe({
      next: () => {
        this.success.set('Backup source created');
        this.resetForm();
        this.loadSources();
        this.isSubmitting.set(false);
        setTimeout(() => this.success.set(''), 3000);
      },
      error: (err) => {
        console.error('Failed to create backup source', err);
        this.error.set('Failed to create backup source');
        this.isSubmitting.set(false);
      },
    });
  }

  deleteSource(source: BackupSource): void {
    this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: `Delete "${source.name}"?`,
        message: 'This will permanently remove this backup source configuration.',
        confirmText: 'Delete',
      },
      closed: (result: boolean) => {
        if (result) {
          this.backupSourceService.delete(source.id).subscribe({
            next: () => {
              this.success.set('Backup source deleted');
              this.loadSources();
              setTimeout(() => this.success.set(''), 3000);
            },
            error: (err) => {
              console.error('Failed to delete backup source', err);
              this.error.set('Failed to delete backup source');
            },
          });
        }
      },
    });
  }

  toggleIndexForm(sourceId: string): void {
    if (this.indexSourceId() === sourceId) {
      this.indexSourceId.set(null);
      this.indexDlistFilename.set('');
    } else {
      this.indexSourceId.set(sourceId);
      this.indexDlistFilename.set('');
    }
  }

  triggerIndex(source: BackupSource): void {
    const filename = this.indexDlistFilename().trim();
    if (!filename) return;

    this.isIndexing.set(true);
    this.error.set('');

    this.backupSourceService.triggerIndex(source.duplicatiBackupId, filename).subscribe({
      next: () => {
        this.success.set(`Indexing triggered for ${source.name}`);
        this.indexSourceId.set(null);
        this.indexDlistFilename.set('');
        this.isIndexing.set(false);
        setTimeout(() => this.success.set(''), 3000);
      },
      error: (err) => {
        console.error('Failed to trigger indexing', err);
        this.error.set('Failed to trigger indexing');
        this.isIndexing.set(false);
      },
    });
  }

  private resetForm(): void {
    this.formName.set('');
    this.formBackupId.set('');
    this.formTargetUrl.set('');
    this.formPassword.set('');
    this.showAddForm.set(false);
  }
}
