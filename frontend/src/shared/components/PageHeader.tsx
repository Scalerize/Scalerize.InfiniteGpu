import type { ReactNode } from "react";

interface PageHeaderProps {
  title: string;
  description?: ReactNode;
  actions?: ReactNode;
  className?: string;
  titleClassName?: string;
  descriptionClassName?: string;
  actionsClassName?: string;
}

const mergeClasses = (...classes: Array<string | undefined | false>) =>
  classes.filter(Boolean).join(" ");

export const PageHeader = ({
  title,
  description,
  actions,
  className,
  titleClassName,
  descriptionClassName,
  actionsClassName,
}: PageHeaderProps) => {
  const headerClassName = mergeClasses(
    "md:sticky md:top-0 md:z-10 md:pb-4",
    className
  );

  const headingClassName = mergeClasses(
    "text-2xl font-semibold text-slate-900 dark:text-white",
    titleClassName
  );

  const descriptionClassNames = mergeClasses(
    "mt-2 text-slate-500 dark:text-slate-400",
    descriptionClassName
  );

  const actionsContainerClassName = mergeClasses(
    "md:ml-6 md:self-end",
    actionsClassName
  );

  const DescriptionComponent =
    typeof description === "string" ? "p" : "div";

  return (
    <header className={headerClassName}>
      <div className="flex flex-col gap-4 md:flex-row md:items-start md:justify-between">
        <div className="flex-1">
          <h2 className={headingClassName}>{title}</h2>
          {description !== undefined && description !== null ? (
            <DescriptionComponent className={descriptionClassNames}>
              {description}
            </DescriptionComponent>
          ) : null}
        </div>
        {actions ? (
          <div className={actionsContainerClassName}>{actions}</div>
        ) : null}
      </div>
    </header>
  );
};