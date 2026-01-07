import { describe, it, expect, beforeEach, vi } from 'vitest';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CopyButton } from '../../components/CopyButton';

describe('CopyButton', () => {
  let mockOnClick: () => void;

  beforeEach(() => {
    mockOnClick = vi.fn();
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('should render button with default text', () => {
      render(<CopyButton onClick={mockOnClick} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toBeInTheDocument();
      expect(button).toHaveTextContent('Copy Blob');
    });

    it('should render button with custom text', () => {
      render(<CopyButton onClick={mockOnClick} text="Start Copy" />);

      expect(screen.getByTestId('start-copy-button')).toHaveTextContent('Start Copy');
    });

    it('should render with tooltip when provided', () => {
      const tooltip = 'Click to copy blob';
      render(<CopyButton onClick={mockOnClick} tooltip={tooltip} />);

      const wrapper = screen.getByTestId('start-copy-button').parentElement;
      expect(wrapper).toHaveAttribute('title', tooltip);
    });
  });

  describe('Click Handler', () => {
    it('should call onClick when clicked', async () => {
      const user = userEvent.setup();
      render(<CopyButton onClick={mockOnClick} />);

      const button = screen.getByTestId('start-copy-button');
      await user.click(button);

      expect(mockOnClick).toHaveBeenCalledOnce();
    });

    it('should not call onClick when disabled', async () => {
      const user = userEvent.setup();
      render(<CopyButton onClick={mockOnClick} disabled={true} />);

      const button = screen.getByTestId('start-copy-button');
      await user.click(button);

      expect(mockOnClick).not.toHaveBeenCalled();
    });

    it('should not call onClick when loading', async () => {
      const user = userEvent.setup();
      render(<CopyButton onClick={mockOnClick} isLoading={true} />);

      const button = screen.getByTestId('start-copy-button');
      await user.click(button);

      expect(mockOnClick).not.toHaveBeenCalled();
    });
  });

  describe('Loading State', () => {
    it('should show spinner when loading', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={true} />);

      const spinner = screen.getByLabelText('Loading');
      expect(spinner).toBeInTheDocument();
      expect(spinner).toHaveClass('spinner');
    });

    it('should display loading text when loading', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={true} />);

      expect(screen.getByTestId('start-copy-button')).toHaveTextContent('Copying...');
    });

    it('should not show spinner when not loading', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={false} />);

      const spinner = screen.queryByLabelText('Loading');
      expect(spinner).not.toBeInTheDocument();
    });

    it('should apply loading class when loading', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={true} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveClass('loading');
    });
  });

  describe('Disabled State', () => {
    it('should apply disabled class when disabled prop is true', () => {
      render(<CopyButton onClick={mockOnClick} disabled={true} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveClass('disabled');
    });

    it('should set disabled attribute when disabled', () => {
      render(<CopyButton onClick={mockOnClick} disabled={true} />);

      const button = screen.getByTestId('start-copy-button') as HTMLButtonElement;
      expect(button.disabled).toBe(true);
    });

    it('should set disabled attribute when loading', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={true} />);

      const button = screen.getByTestId('start-copy-button') as HTMLButtonElement;
      expect(button.disabled).toBe(true);
    });
  });

  describe('ARIA Attributes', () => {
    it('should have aria-busy true when loading', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={true} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveAttribute('aria-busy', 'true');
    });

    it('should have aria-busy false when not loading', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={false} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveAttribute('aria-busy', 'false');
    });

    it('should have aria-disabled true when disabled', () => {
      render(<CopyButton onClick={mockOnClick} disabled={true} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveAttribute('aria-disabled', 'true');
    });

    it('should have aria-disabled false when not disabled', () => {
      render(<CopyButton onClick={mockOnClick} disabled={false} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveAttribute('aria-disabled', 'false');
    });

    it('should have aria-disabled true when loading (via isDisabled logic)', () => {
      render(<CopyButton onClick={mockOnClick} isLoading={true} />);

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveAttribute('aria-disabled', 'true');
    });
  });

  describe('Prop Combinations', () => {
    it('should handle disabled and loading together', () => {
      render(<CopyButton onClick={mockOnClick} disabled={true} isLoading={true} />);

      const button = screen.getByTestId('start-copy-button') as HTMLButtonElement;
      expect(button.disabled).toBe(true);
      expect(button).toHaveClass('disabled');
      expect(button).toHaveClass('loading');
      expect(screen.getByLabelText('Loading')).toBeInTheDocument();
    });

    it('should handle all props together', () => {
      render(
        <CopyButton
          onClick={mockOnClick}
          disabled={false}
          isLoading={false}
          tooltip="Test tooltip"
          text="Custom Text"
        />
      );

      const button = screen.getByTestId('start-copy-button');
      expect(button).toHaveTextContent('Custom Text');
      expect(button.parentElement).toHaveAttribute('title', 'Test tooltip');
      expect(button).not.toHaveClass('disabled');
      expect(button).not.toHaveClass('loading');
    });
  });
});
