import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface ApplicationDto {
  id: string;
  name: string;
  description?: string;
  applicationCode: string;
  applicationUrl: string;
  iconUrl?: string;
  isActive: boolean;
  createdAtUtc: string;
  isAssignedToCurrentUser: boolean;
}

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

export interface AssignApplicationRequest {
  userId: string;
  applicationId: string;
}

export interface AssignApplicationByEmailRequest {
  email: string;
  applicationId: string;
}

export interface RevokeApplicationRequest {
  userId: string;
  applicationId: string;
}

export interface UserApplicationDto {
  id: number;
  userId: string;
  userEmail?: string;
  applicationId: string;
  applicationName: string;
  applicationCode: string;
  applicationUrl: string;
  iconUrl?: string;
  isActive: boolean;
  assignedAtUtc: string;
  assignedBy?: string;
}

/** Client for the Application Catalog / Access Management Portal endpoints (api/applications). */
@Injectable({ providedIn: 'root' })
export class ApplicationService {
  private readonly baseUrl = '/api/applications';

  constructor(private http: HttpClient) {}

  search(searchTerm: string, page: number, pageSize: number): Observable<PagedResult<ApplicationDto>> {
    const params = new URLSearchParams({
      page: String(page),
      pageSize: String(pageSize)
    });
    if (searchTerm) {
      params.set('searchTerm', searchTerm);
    }

    return this.http.get<PagedResult<ApplicationDto>>(`${this.baseUrl}/search?${params.toString()}`, {
      withCredentials: true
    });
  }

  assign(request: AssignApplicationRequest): Observable<UserApplicationDto> {
    return this.http.post<UserApplicationDto>(`${this.baseUrl}/assign`, request, { withCredentials: true });
  }

  assignByEmail(request: AssignApplicationByEmailRequest): Observable<UserApplicationDto> {
    return this.http.post<UserApplicationDto>(`${this.baseUrl}/assign-by-email`, request, { withCredentials: true });
  }

  revoke(request: RevokeApplicationRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/revoke`, request, { withCredentials: true });
  }

  myApplications(): Observable<UserApplicationDto[]> {
    return this.http.get<UserApplicationDto[]>(`${this.baseUrl}/my-applications`, { withCredentials: true });
  }

  launch(applicationId: string): Observable<{ launchUrl: string }> {
    return this.http.get<{ launchUrl: string }>(`${this.baseUrl}/${applicationId}/launch`, { withCredentials: true });
  }
}
