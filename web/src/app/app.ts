import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  ElementRef,
  inject,
  Injector,
  signal,
  viewChild,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { MatButtonModule } from '@angular/material/button';
import { MatToolbarModule } from '@angular/material/toolbar';
import {
  ActivatedRouteSnapshot,
  NavigationEnd,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { filter } from 'rxjs';
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
  private readonly injector = inject(Injector);
  private readonly main = viewChild.required<ElementRef<HTMLElement>>('main');

  /** Pages that lay themselves out edge to edge (route data `fullBleed`) skip the content column. */
  protected readonly fullBleed = signal(false);

  protected readonly logoutFailed = signal(false);
  protected readonly loggingOut = signal(false);

  constructor() {
    // After moving to a different page, put focus at the start of its content (its heading)
    // so keyboard and screen-reader users continue from there. A changed query string (search,
    // paging) is the same page and keeps focus where the user is; the first load is left alone.
    let previousPath: string | null = null;
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(),
      )
      .subscribe((event) => {
        this.fullBleed.set(deepestRoute(this.router.routerState.snapshot.root).data['fullBleed'] === true);
        const path = event.urlAfterRedirects.split(/[?#]/, 1)[0];
        const isPageChange = previousPath !== null && path !== previousPath;
        previousPath = path;
        if (isPageChange) afterNextRender(() => this.focusMainContent(), { injector: this.injector });
      });
  }

  protected skipToMainContent(event: Event): void {
    event.preventDefault();
    this.focusMainContent();
  }

  protected async logout(): Promise<void> {
    this.loggingOut.set(true);
    const revoked = await this.auth.logout();
    this.loggingOut.set(false);
    this.logoutFailed.set(!revoked);
    if (revoked) await this.router.navigateByUrl('/');
  }

  /** Focuses the page heading, or the main region while the heading is still loading. */
  private focusMainContent(): void {
    const main = this.main().nativeElement;
    const heading = main.querySelector<HTMLElement>('h1');
    const target = heading ?? main;
    target.setAttribute('tabindex', '-1');
    target.focus();
  }
}

function deepestRoute(route: ActivatedRouteSnapshot): ActivatedRouteSnapshot {
  return route.firstChild ? deepestRoute(route.firstChild) : route;
}
