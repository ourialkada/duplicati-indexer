import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface BackupSource {
  id: string;
  name: string;
  duplicatiBackupId: string;
  targetUrl: string;
  encryptionPassword?: string;
  createdAt: string;
  lastParsedVersion?: string;
  isPaused: boolean;
}

export interface CreateBackupSourceRequest {
  name: string;
  duplicatiBackupId: string;
  targetUrl: string;
  encryptionPassword?: string;
}

@Injectable({ providedIn: 'root' })
export class BackupSourceService {
  private http = inject(HttpClient);

  getAll(): Observable<BackupSource[]> {
    return this.http.get<BackupSource[]>('/api/backup-sources');
  }

  create(request: CreateBackupSourceRequest): Observable<BackupSource> {
    return this.http.post<BackupSource>('/api/backup-sources', request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`/api/backup-sources/${id}`);
  }

  triggerIndex(backupId: string, dlistFilename: string): Observable<unknown> {
    return this.http.post('/api/messages/backup-version-created', {
      backupId,
      dlistFilename
    });
  }
}
