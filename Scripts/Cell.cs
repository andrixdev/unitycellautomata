
using System.Runtime.InteropServices;

class Cell
{
	public static int lifeRange = 50;
	private byte status;
	private int xNbr, yNbr, zNbr;
	private int lifespan;

	//Accesseurs et mutateurs
	public Cell(int xNbr, int yNbr, int zNbr, byte status)
	{
		this.xNbr = xNbr;
		this.yNbr = yNbr;
		this.zNbr = zNbr;
		this.status = status;
		this.lifespan = 0;
	}

	public byte getStatus()
	{
		return status;
	}

	public void changeStatus(byte new_status)
	{
		byte past_status = this.status;
		this.status = new_status;

		if(past_status == 0 && this.status > 0)
        {
			this.resetLifespan();
        }
		else if(past_status > 0 && this.status == 0)
        {
			this.resetLifespan();
        }
		else if(past_status > 0 && this.status > 0)
        {
			this.incrLifespan();
        }
	}

	public int getLifespan()
	{
		return lifespan;
	}

	public void incrLifespan()
    {
		this.lifespan++;
		if (this.lifespan > Cell.lifeRange)
			this.lifespan = Cell.lifeRange;
    }

	public void resetLifespan()
	{
		this.lifespan = 0;
	}
	
	public Cell Clone()
	{
		return new Cell(this.xNbr,this.yNbr,this.zNbr,this.status);
	}

}
