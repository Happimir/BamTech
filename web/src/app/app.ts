import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthStore } from './core/auth/auth.store';

@Component({
  selector: 'acts-root',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [RouterOutlet, RouterLink, RouterLinkActive, MatButtonModule, MatToolbarModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {
  protected readonly auth = inject(AuthStore);
  private readonly router = inject(Router);

  protected readonly logoutFailed = signal(false);
  protected readonly loggingOut = signal(false);

  protected async logout(): Promise<void> {
    this.loggingOut.set(true);
    const revoked = await this.auth.logout();
    this.loggingOut.set(false);
    this.logoutFailed.set(!revoked);
    if (revoked) await this.router.navigateByUrl('/login');
  }
}
